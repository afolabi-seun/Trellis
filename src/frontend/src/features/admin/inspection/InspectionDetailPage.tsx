import { Badge, Button, Group, Stack, Table, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { useNavigate, useParams } from "react-router-dom";
import { inspectionsApi } from "../../../api/inspections";

const inspectionStatusColor: Record<string, string> = {
  SCHEDULED: "gray.8",
  IN_PROGRESS: "blue.8",
  CLOSED: "success.8",
};

const itemStatusColor: Record<string, string> = {
  PENDING: "gray.6",
  PASS: "success.8",
  FAIL: "red.8",
  NOT_APPLICABLE: "gray.6",
};

/** BDR-INSP-004's structured report, staff-side read view — the same InspectionReportResponse the API returns, rendered directly. */
export function InspectionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const inspectionQuery = useQuery({
    queryKey: ["inspection", "inspections", id],
    queryFn: () => inspectionsApi.getById(id!),
    enabled: !!id,
  });

  const handleDownload = async (photoId: string, fileName: string) => {
    const blob = await inspectionsApi.downloadPhoto(id!, photoId);
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  };

  const inspection = inspectionQuery.data;

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Stack gap={0}>
          <Button
            variant="subtle"
            size="compact-sm"
            onClick={() => navigate("/admin/inspections")}
            w="fit-content"
          >
            ← Inspections
          </Button>
          <Title order={2}>Inspection report</Title>
        </Stack>
        {inspection?.status && (
          <Badge color={inspectionStatusColor[inspection.status] ?? "gray.6"}>
            {inspection.status}
          </Badge>
        )}
      </Group>

      <Stack
        gap="xs"
        p="md"
        style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
      >
        <Text c="gray.7" size="sm">
          Scheduled:{" "}
          {inspection?.scheduledAt ? new Date(inspection.scheduledAt).toLocaleString() : "—"}
        </Text>
        {inspection?.latitude != null && inspection?.longitude != null && (
          <Text c="gray.7" size="sm">
            Location: {inspection.latitude.toFixed(5)}, {inspection.longitude.toFixed(5)}
          </Text>
        )}
      </Stack>

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
            {inspection?.items?.map((item) => (
              <Table.Tr key={item.inspectionItemId}>
                <Table.Td>{item.label}</Table.Td>
                <Table.Td>
                  <Badge color={itemStatusColor[item.status ?? ""] ?? "gray.6"}>
                    {item.status}
                  </Badge>
                </Table.Td>
                <Table.Td>{item.notes ?? "—"}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Stack>

      <Stack gap="md">
        <Title order={3}>Photos</Title>
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>File name</Table.Th>
              <Table.Th>Scan status</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {inspection?.photos?.map((photo) => (
              <Table.Tr key={photo.inspectionPhotoId}>
                <Table.Td>{photo.fileName}</Table.Td>
                <Table.Td>{photo.scanStatus}</Table.Td>
                <Table.Td>
                  <Button
                    size="xs"
                    variant="subtle"
                    disabled={photo.scanStatus !== "CLEAN"}
                    onClick={() =>
                      photo.inspectionPhotoId &&
                      photo.fileName &&
                      handleDownload(photo.inspectionPhotoId, photo.fileName)
                    }
                  >
                    Download
                  </Button>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        {inspection?.photos?.length === 0 && <Text c="gray.7">No photos yet.</Text>}
      </Stack>
    </Stack>
  );
}
