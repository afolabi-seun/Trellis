import { beforeEach, describe, expect, it } from "vitest";
import { offlineDb } from "../../../src/offline/db";

describe("offlineDb", () => {
  beforeEach(async () => {
    await offlineDb.inspections.clear();
    await offlineDb.photos.clear();
  });

  it("round-trips a queued inspection", async () => {
    await offlineDb.inspections.put({
      workItemId: "wi-1",
      idempotencyKey: "key-1",
      items: [
        { inspectionItemId: "item-1", label: "Smoke Detectors", status: "PASS", notes: "ok" },
      ],
      status: "draft",
      updatedAt: new Date().toISOString(),
    });

    const stored = await offlineDb.inspections.get("wi-1");

    expect(stored).toBeDefined();
    expect(stored?.items).toHaveLength(1);
    expect(stored?.items[0].status).toBe("PASS");
  });

  it("keeps photo blobs in a separate table, queryable by workItemId", async () => {
    await offlineDb.photos.put({
      clientPhotoId: "photo-1",
      workItemId: "wi-1",
      fileName: "a.jpg",
      contentType: "image/jpeg",
      blob: new Blob(["fake"], { type: "image/jpeg" }),
      capturedAt: new Date().toISOString(),
    });
    await offlineDb.photos.put({
      clientPhotoId: "photo-2",
      workItemId: "wi-2",
      fileName: "b.jpg",
      contentType: "image/jpeg",
      blob: new Blob(["fake"], { type: "image/jpeg" }),
      capturedAt: new Date().toISOString(),
    });

    const forWi1 = await offlineDb.photos.where("workItemId").equals("wi-1").toArray();

    expect(forWi1).toHaveLength(1);
    expect(forWi1[0].clientPhotoId).toBe("photo-1");
  });

  it("deletes an inspection and its photos independently", async () => {
    await offlineDb.inspections.put({
      workItemId: "wi-1",
      idempotencyKey: "key-1",
      items: [],
      status: "ready",
      updatedAt: new Date().toISOString(),
    });
    await offlineDb.photos.put({
      clientPhotoId: "photo-1",
      workItemId: "wi-1",
      fileName: "a.jpg",
      contentType: "image/jpeg",
      blob: new Blob(["fake"]),
      capturedAt: new Date().toISOString(),
    });

    await offlineDb.photos.where("workItemId").equals("wi-1").delete();
    await offlineDb.inspections.delete("wi-1");

    expect(await offlineDb.inspections.get("wi-1")).toBeUndefined();
    expect(await offlineDb.photos.where("workItemId").equals("wi-1").toArray()).toHaveLength(0);
  });
});
