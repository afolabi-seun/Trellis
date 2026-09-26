import {
  Alert,
  Badge,
  Button,
  Group,
  Select,
  Stack,
  Table,
  Text,
  Textarea,
  Title,
} from "@mantine/core";
import { Dropzone } from "@mantine/dropzone";
import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { inspectionsApi } from "../../api/inspections";
import type { components } from "../../api/schema";
import { offlineDb, type QueuedInspection, type QueuedInspectionPhoto } from "../../offline/db";
import { flushQueue } from "../../offline/syncCoordinator";

type VendorInspectionView = components["schemas"]["VendorInspectionView"];

const itemStatusOptions = [
  { value: "PENDING", label: "Pending" },
  { value: "PASS", label: "Pass" },
  { value: "FAIL", label: "Fail" },
  { value: "NOT_APPLICABLE", label: "N/A" },
];

/**
 * BDR-INSP-002/003 — the offline-first capture flow. Works entirely against a local Dexie draft
 * (frontend_architecture.md): checklist edits and photo captures are written to IndexedDB the
 * moment they're entered, never held in memory waiting for a network check. "Finish inspection"
 * is the one point a network call is even attempted — and if there's no connectivity, it's queued
 * instead of failing, synced automatically by syncCoordinator.ts the moment the browser's `online`
 * event fires.
 */
export function VendorInspectionCapturePage() {
  const { workItemId } = useParams<{ workItemId: string }>();
  const navigate = useNavigate();
  const [draft, setDraft] = useState<QueuedInspection | null>(null);
  const [photos, setPhotos] = useState<QueuedInspectionPhoto[]>([]);
  const [assignmentStatus, setAssignmentStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [startPending, setStartPending] = useState(false);
  const [finishPending, setFinishPending] = useState(false);
  const [synced, setSynced] = useState(false);
  const [closedView, setClosedView] = useState<VendorInspectionView | null>(null);

  const reloadFromDexie = useCallback(async () => {
    if (!workItemId) return;
    const existing = await offlineDb.inspections.get(workItemId);
    setDraft(existing ?? null);
    const existingPhotos = await offlineDb.photos.where("workItemId").equals(workItemId).toArray();
    setPhotos(existingPhotos);
    return existing;
  }, [workItemId]);

  // A "Finish inspection" attempted while offline queues the draft (status "ready"/"failed") and
  // returns immediately — the actual sync happens later, in the background, driven by
  // syncCoordinator's own `online` listener (registered once in main.tsx, independent of whether
  // this page is even mounted). flushQueue() is single-flight-coalesced, so calling it again here
  // is safe and free if that background flush is already running or just finished; this is what
  // lets the page notice a sync it didn't itself trigger and show "Inspection synced".
  useEffect(() => {
    if (!draft || draft.status === "draft") return;

    const checkForSync = () => {
      void (async () => {
        await flushQueue();
        const stillQueued = workItemId ? await offlineDb.inspections.get(workItemId) : null;
        if (!stillQueued) {
          setSynced(true);
        } else {
          await reloadFromDexie();
        }
      })();
    };

    window.addEventListener("online", checkForSync);
    // Belt-and-suspenders alongside the `online` listener — syncCoordinator.ts's own doc comment
    // explains why a poll is needed at all (some environments never dispatch a real `online`
    // event even once connectivity is actually back).
    const pollId = window.setInterval(checkForSync, 3000);
    return () => {
      window.removeEventListener("online", checkForSync);
      window.clearInterval(pollId);
    };
  }, [draft, workItemId, reloadFromDexie]);

  useEffect(() => {
    if (!workItemId) return;

    (async () => {
      setLoading(true);
      setLoadError(null);

      const existing = await reloadFromDexie();
      if (existing) {
        setLoading(false);
        return;
      }

      if (!navigator.onLine) {
        setLoadError(
          "This inspection hasn't been opened yet — connect once to load it before going offline.",
        );
        setLoading(false);
        return;
      }

      try {
        const server = await inspectionsApi.getForVendor(workItemId);
        setAssignmentStatus(server.status ?? null);

        // Already submitted — show the real, server-side result (including any photos, which
        // only ever exist server-side once synced) rather than starting a fresh, empty editable
        // draft that would misleadingly show zero photos for a closed inspection.
        if (server.status === "CLOSED") {
          setClosedView(server);
          setLoading(false);
          return;
        }

        const newDraft: QueuedInspection = {
          workItemId,
          idempotencyKey: crypto.randomUUID(),
          items: (server.items ?? []).map((i) => ({
            inspectionItemId: i.inspectionItemId!,
            label: i.label ?? "",
            status: i.status ?? "PENDING",
            notes: i.notes ?? undefined,
          })),
          status: "draft",
          updatedAt: new Date().toISOString(),
        };
        await offlineDb.inspections.put(newDraft);
        setDraft(newDraft);
        setPhotos([]);
      } catch (err) {
        setLoadError(err instanceof Error ? err.message : "Could not load this inspection.");
      } finally {
        setLoading(false);
      }
    })();
  }, [workItemId, reloadFromDexie]);

  const updateItem = async (
    inspectionItemId: string,
    changes: Partial<{ status: string; notes: string }>,
  ) => {
    if (!draft) return;
    const nextItems = draft.items.map((i) =>
      i.inspectionItemId === inspectionItemId ? { ...i, ...changes } : i,
    );
    const next: QueuedInspection = {
      ...draft,
      items: nextItems,
      updatedAt: new Date().toISOString(),
    };
    await offlineDb.inspections.put(next);
    setDraft(next);
  };

  const handleAddPhoto = async (files: File[]) => {
    if (!workItemId || files.length === 0) return;
    const file = files[0];
    const photo: QueuedInspectionPhoto = {
      clientPhotoId: crypto.randomUUID(),
      workItemId,
      fileName: file.name,
      contentType: file.type || "image/jpeg",
      blob: file,
      capturedAt: new Date().toISOString(),
    };

    if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          void offlineDb.photos.put({
            ...photo,
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
          });
        },
        () => {
          void offlineDb.photos.put(photo);
        },
        { timeout: 5000 },
      );
    } else {
      await offlineDb.photos.put(photo);
    }

    await new Promise((resolve) => setTimeout(resolve, 100));
    await reloadFromDexie();
  };

  const handleStart = async () => {
    setStartPending(true);
    try {
      const result = await inspectionsApi.startForVendor(workItemId!);
      setAssignmentStatus(result.status ?? "IN_PROGRESS");
    } finally {
      setStartPending(false);
    }
  };

  const handleFinish = async () => {
    if (!draft || !workItemId) return;
    setFinishPending(true);
    try {
      let latitude: number | undefined;
      let longitude: number | undefined;
      if (navigator.geolocation) {
        await new Promise<void>((resolve) => {
          navigator.geolocation.getCurrentPosition(
            (position) => {
              latitude = position.coords.latitude;
              longitude = position.coords.longitude;
              resolve();
            },
            () => resolve(),
            { timeout: 5000 },
          );
        });
      }

      await offlineDb.inspections.put({
        ...draft,
        status: "ready",
        latitude,
        longitude,
        updatedAt: new Date().toISOString(),
      });
      await flushQueue();

      const stillQueued = await offlineDb.inspections.get(workItemId);
      if (!stillQueued) {
        setSynced(true);
      } else {
        await reloadFromDexie();
      }
    } finally {
      setFinishPending(false);
    }
  };

  if (loading) {
    return (
      <Stack p="lg">
        <Text c="gray.7">Loading…</Text>
      </Stack>
    );
  }

  if (synced) {
    return (
      <Stack p="lg" gap="md">
        <Alert color="green" title="Inspection synced">
          This inspection has been submitted and closed.
        </Alert>
        <Button onClick={() => navigate("/vendor")} w="fit-content">
          Back to assignments
        </Button>
      </Stack>
    );
  }

  if (closedView) {
    return (
      <Stack gap="lg" p="lg">
        <Group justify="space-between">
          <Button
            variant="subtle"
            size="compact-sm"
            onClick={() => navigate("/vendor")}
            w="fit-content"
          >
            ← Assignments
          </Button>
          <Badge color="success.8">CLOSED</Badge>
        </Group>
        <Title order={2}>Inspection</Title>
        <Stack gap="md">
          <Title order={3}>Checklist</Title>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Item</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Notes</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {closedView.items?.map((item) => (
                <Table.Tr key={item.inspectionItemId}>
                  <Table.Td>{item.label}</Table.Td>
                  <Table.Td>{item.status}</Table.Td>
                  <Table.Td>{item.notes ?? "—"}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Stack>
        <Stack gap="md">
          <Title order={3}>Photos</Title>
          <Table striped>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>File name</Table.Th>
                <Table.Th>Scan status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {closedView.photos?.map((photo) => (
                <Table.Tr key={photo.inspectionPhotoId}>
                  <Table.Td>{photo.fileName}</Table.Td>
                  <Table.Td>{photo.scanStatus}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Stack>
      </Stack>
    );
  }

  if (loadError || !draft) {
    return (
      <Stack p="lg">
        <Alert color="red" title="Could not open inspection">
          {loadError ?? "Something went wrong."}
        </Alert>
      </Stack>
    );
  }

  const queueStateLabel: Record<QueuedInspection["status"], string> = {
    draft: "In progress",
    ready: "Queued to sync",
    syncing: "Syncing…",
    synced: "Synced",
    failed: navigator.onLine
      ? "Could not sync — will retry"
      : "Queued — will sync when back online",
  };

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Button
          variant="subtle"
          size="compact-sm"
          onClick={() => navigate("/vendor")}
          w="fit-content"
        >
          ← Assignments
        </Button>
        <Badge color={draft.status === "failed" ? "yellow.8" : "gray.8"}>
          {queueStateLabel[draft.status]}
        </Badge>
      </Group>

      <Title order={2}>Inspection</Title>

      {assignmentStatus === "SCHEDULED" && (
        <Button onClick={handleStart} loading={startPending} w="fit-content">
          Start inspection
        </Button>
      )}

      <Stack gap="md">
        <Title order={3}>Checklist</Title>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Item</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Notes</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {draft.items.map((item) => (
              <Table.Tr key={item.inspectionItemId}>
                <Table.Td>{item.label}</Table.Td>
                <Table.Td>
                  <Select
                    aria-label={`Status for ${item.label}`}
                    data={itemStatusOptions}
                    value={item.status}
                    onChange={(value) =>
                      value && void updateItem(item.inspectionItemId, { status: value })
                    }
                    w={140}
                  />
                </Table.Td>
                <Table.Td>
                  <Textarea
                    aria-label={`Notes for ${item.label}`}
                    value={item.notes ?? ""}
                    onChange={(e) =>
                      void updateItem(item.inspectionItemId, { notes: e.currentTarget.value })
                    }
                    autosize
                    minRows={1}
                  />
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Stack>

      <Stack gap="md">
        <Title order={3}>Photos</Title>
        <Table striped>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>File name</Table.Th>
              <Table.Th>Geotagged</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {photos.map((photo) => (
              <Table.Tr key={photo.clientPhotoId}>
                <Table.Td>{photo.fileName}</Table.Td>
                <Table.Td>{photo.latitude != null ? "Yes" : "No"}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>

        <Dropzone
          onDrop={handleAddPhoto}
          maxFiles={1}
          accept={{
            "image/jpeg": [".jpg", ".jpeg"],
            "image/png": [".png"],
            "image/heic": [".heic"],
          }}
          inputProps={{ "aria-label": "Inspection photo" }}
        >
          <Text ta="center">Drag a photo here, or click to select one</Text>
        </Dropzone>
      </Stack>

      <Button onClick={handleFinish} loading={finishPending} disabled={draft.status === "syncing"}>
        Finish inspection
      </Button>
    </Stack>
  );
}
