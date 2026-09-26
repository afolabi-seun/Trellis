import { beforeEach, describe, expect, it, vi } from "vitest";
import { offlineDb } from "../../../src/offline/db";
import { flushQueue } from "../../../src/offline/syncCoordinator";

vi.mock("../../../src/api/inspections", () => ({
  inspectionsApi: { completeForVendor: vi.fn() },
}));

const { inspectionsApi } = await import("../../../src/api/inspections");
const completeForVendor = vi.mocked(inspectionsApi.completeForVendor);

describe("syncCoordinator", () => {
  beforeEach(async () => {
    await offlineDb.inspections.clear();
    await offlineDb.photos.clear();
    completeForVendor.mockReset();
  });

  it("submits a ready inspection and removes it from the queue on success", async () => {
    completeForVendor.mockResolvedValue({ inspectionId: "wi-1", status: "CLOSED" });
    await offlineDb.inspections.put({
      workItemId: "wi-1",
      idempotencyKey: "key-1",
      items: [{ inspectionItemId: "item-1", label: "Smoke Detectors", status: "PASS" }],
      status: "ready",
      updatedAt: new Date().toISOString(),
    });

    await flushQueue();

    expect(completeForVendor).toHaveBeenCalledTimes(1);
    expect(completeForVendor).toHaveBeenCalledWith("wi-1", expect.anything(), "key-1");
    expect(await offlineDb.inspections.get("wi-1")).toBeUndefined();
  });

  it("automatically retries a failed sync on the next flush, reusing the same idempotency key", async () => {
    completeForVendor.mockRejectedValueOnce(new Error("network error"));
    await offlineDb.inspections.put({
      workItemId: "wi-1",
      idempotencyKey: "key-1",
      items: [],
      status: "ready",
      updatedAt: new Date().toISOString(),
    });

    await flushQueue();

    const stillQueued = await offlineDb.inspections.get("wi-1");
    expect(stillQueued?.status).toBe("failed");
    expect(stillQueued?.idempotencyKey).toBe("key-1");

    // "failed" is a retry candidate, not a terminal state — the next flush must pick it up on its
    // own, with no manual status reset (the real bug this test caught: runFlush() originally only
    // ever queried for status "ready", so a failed attempt was silently never retried).
    completeForVendor.mockResolvedValueOnce({ inspectionId: "wi-1", status: "CLOSED" });
    await flushQueue();

    expect(completeForVendor).toHaveBeenLastCalledWith("wi-1", expect.anything(), "key-1");
    expect(await offlineDb.inspections.get("wi-1")).toBeUndefined();
  });

  it("coalesces overlapping flush calls into a single in-flight run", async () => {
    let resolveCall: (() => void) | undefined;
    completeForVendor.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveCall = () => resolve({ inspectionId: "wi-1", status: "CLOSED" });
        }),
    );
    await offlineDb.inspections.put({
      workItemId: "wi-1",
      idempotencyKey: "key-1",
      items: [],
      status: "ready",
      updatedAt: new Date().toISOString(),
    });

    const first = flushQueue();
    const second = flushQueue();

    await vi.waitFor(() => expect(resolveCall).toBeDefined());
    resolveCall?.();
    await Promise.all([first, second]);

    expect(completeForVendor).toHaveBeenCalledTimes(1);
  });
});
