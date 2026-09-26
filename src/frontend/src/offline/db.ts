import Dexie, { type Table } from "dexie";

/** frontend_architecture.md's "Offline Inspection Capture" — checklist responses, notes, and geolocation, written to IndexedDB the moment they're entered, not held in memory waiting for a network check. */
export interface QueuedInspectionItem {
  inspectionItemId: string;
  label: string;
  status: string;
  notes?: string;
}

export interface QueuedInspection {
  /** The inspection's own id — also the vendor_assignment work item id, and the Dexie PK. */
  workItemId: string;
  /** Minted once, when the vendor first opens this inspection for offline capture — reused for every retry of this same submission, never regenerated per network attempt (decision 8's own caveat: only a fresh key re-attempts a genuinely different payload). */
  idempotencyKey: string;
  items: QueuedInspectionItem[];
  latitude?: number;
  longitude?: number;
  status: "draft" | "ready" | "syncing" | "synced" | "failed";
  lastError?: string;
  updatedAt: string;
}

/** Kept in a separate table from QueuedInspection so editing checklist state doesn't rewrite large blob records on every keystroke. */
export interface QueuedInspectionPhoto {
  clientPhotoId: string;
  workItemId: string;
  inspectionItemId?: string;
  fileName: string;
  contentType: string;
  blob: Blob;
  latitude?: number;
  longitude?: number;
  capturedAt: string;
}

export class TrellisOfflineDb extends Dexie {
  inspections!: Table<QueuedInspection, string>;
  photos!: Table<QueuedInspectionPhoto, string>;

  constructor() {
    super("trellis-offline");
    this.version(1).stores({
      inspections: "workItemId, status",
      photos: "clientPhotoId, workItemId",
    });
  }
}

export const offlineDb = new TrellisOfflineDb();
