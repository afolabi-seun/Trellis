import { inspectionsApi, type InspectionCompletePayload } from "../api/inspections";
import { offlineDb } from "./db";

/**
 * frontend_architecture.md's "Sync on reconnect" — a queued-submission pattern: each completed
 * inspection is pushed the moment connectivity returns, using the Background Sync API where
 * supported and a connectivity-change listener with retry as the fallback everywhere else.
 *
 * Single-flight-coalesced exactly like client.ts's own refreshOnce() — an `online` event firing
 * while a previous flush is still running must not start a second overlapping flush.
 *
 * Token readiness: apiFetch's existing 401 → refreshOnce() → retry path (client.ts) is the same
 * safety net a normal page navigation already gets — by the time this ever fires (the app has to
 * be mounted for either the `online` listener or a Background Sync event to reach it),
 * AuthContext's bootstrapSession() has already run via the httpOnly refresh cookie, so no new
 * token-handling code is needed here.
 */
let flushInFlight: Promise<void> | null = null;

export function flushQueue(): Promise<void> {
  flushInFlight ??= runFlush().finally(() => {
    flushInFlight = null;
  });
  return flushInFlight;
}

async function runFlush(): Promise<void> {
  // "failed" is a retry candidate, not a terminal state — a previous attempt that hit a network
  // error (the expected case while offline) must be picked up again on the next flush, not
  // silently ignored forever just because a prior try already flipped it away from "ready".
  const pending = await offlineDb.inspections.where("status").anyOf(["ready", "failed"]).toArray();

  for (const inspection of pending) {
    await offlineDb.inspections.update(inspection.workItemId, { status: "syncing" });

    try {
      const photos = await offlineDb.photos
        .where("workItemId")
        .equals(inspection.workItemId)
        .toArray();
      const payload: InspectionCompletePayload = {
        items: inspection.items.map((i) => ({
          inspectionItemId: i.inspectionItemId,
          status: i.status,
          notes: i.notes,
        })),
        latitude: inspection.latitude,
        longitude: inspection.longitude,
        photos: photos.map((p) => ({
          file: new File([p.blob], p.fileName, { type: p.contentType }),
          fileName: p.fileName,
          inspectionItemId: p.inspectionItemId,
          latitude: p.latitude,
          longitude: p.longitude,
          capturedAt: p.capturedAt,
        })),
      };

      await inspectionsApi.completeForVendor(
        inspection.workItemId,
        payload,
        inspection.idempotencyKey,
      );

      await offlineDb.photos.where("workItemId").equals(inspection.workItemId).delete();
      await offlineDb.inspections.delete(inspection.workItemId);
    } catch (error) {
      await offlineDb.inspections.update(inspection.workItemId, {
        status: "failed",
        lastError: error instanceof Error ? error.message : "Sync failed.",
      });
    }
  }
}

/**
 * Called once from main.tsx. The `online` listener is the reliable mechanism (works in every
 * browser, including iOS/Safari which has no Background Sync API at all); registering a
 * Background Sync tag is a best-effort enhancement on top of it, not a replacement — the current
 * service worker (vite-plugin-pwa's generated one) has no custom `sync` event handler, so this
 * registration alone doesn't wake a backgrounded tab. A dedicated injectManifest-strategy service
 * worker with a real sync handler is a documented follow-up, not required for the connectivity
 * -listener path to satisfy BDR-INSP-003/BDR-NFR-001 today.
 */
const POLL_INTERVAL_MS = 5000;

export function initSyncCoordinator(): void {
  window.addEventListener("online", () => {
    void flushQueue();
  });

  // Belt-and-suspenders alongside the `online` listener above: some environments (confirmed
  // empirically — Playwright's own `context.setOffline(false)` network-condition emulation, which
  // doesn't always dispatch a real `online` DOM event) never fire it at all, even though
  // `navigator.onLine` itself does flip back to true. A cheap poll is the simplest way to
  // guarantee the "syncs automatically the moment connectivity returns" exit criterion actually
  // holds regardless of which mechanism the browser uses to signal reconnection.
  setInterval(() => {
    if (navigator.onLine) {
      void flushQueue();
    }
  }, POLL_INTERVAL_MS);

  if (navigator.onLine) {
    void flushQueue();
  }

  void navigator.serviceWorker?.ready
    .then((registration) => {
      const syncManager = (
        registration as ServiceWorkerRegistration & {
          sync?: { register(tag: string): Promise<void> };
        }
      ).sync;
      return syncManager?.register("inspection-sync");
    })
    .catch(() => {
      // Background Sync isn't supported (Safari/iOS) or registration failed — the `online`
      // listener above is already the real mechanism, so this is silently non-fatal.
    });
}
