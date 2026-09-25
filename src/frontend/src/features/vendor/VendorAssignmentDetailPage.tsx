import { Alert, Badge, Button, Group, Select, Stack, Table, Text, Title } from "@mantine/core";
import { Dropzone } from "@mantine/dropzone";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { documentsApi } from "../../api/documents";
import { TrellisApiError } from "../../api/client";
import { vendorAssignmentsApi } from "../../api/vendorAssignments";
import { dangerAlertStyles } from "../../theme/dangerStyles";

const assignmentStatusColor: Record<string, string> = {
  ASSIGNED: "gray.8",
  IN_PROGRESS: "blue.8",
  COMPLETED: "success.8",
  CLOSED: "gray.8",
};

const documentTypeOptions = [
  { value: "INSPECTION_REPORT", label: "Inspection report" },
  { value: "INVOICE", label: "Invoice" },
  { value: "OTHER", label: "Other" },
];

/** BDR-VEN-002 — this is VendorWorkflowInstanceView, not the tenant's own WorkflowInstanceResponse: no cost/department fields ever reach this page, by the API's own response shape. */
export function VendorAssignmentDetailPage() {
  const { workItemId } = useParams<{ workItemId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<string | null>("OTHER");

  const detailQuery = useQuery({
    queryKey: ["vendor-portal", "assignments", workItemId],
    queryFn: () => vendorAssignmentsApi.getForVendor(workItemId!),
    enabled: !!workItemId,
  });

  const evidenceQuery = useQuery({
    queryKey: ["vendor-portal", "assignments", workItemId, "documents"],
    queryFn: () => documentsApi.listByWorkflowInstance(workItemId!),
    enabled: !!workItemId,
  });

  const startMutation = useMutation({
    mutationFn: () => vendorAssignmentsApi.start(workItemId!),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["vendor-portal", "assignments", workItemId] }),
  });

  const completeMutation = useMutation({
    mutationFn: () => vendorAssignmentsApi.complete(workItemId!),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["vendor-portal", "assignments", workItemId] }),
  });

  const uploadMutation = useMutation({
    mutationFn: () => {
      if (!file || !documentType) {
        throw new Error("A file and document type are required.");
      }
      return documentsApi.createForWorkflowInstance(workItemId!, { documentType, file });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["vendor-portal", "assignments", workItemId, "documents"],
      });
      setFile(null);
    },
  });

  const handleDownload = async (documentId: string, fileName: string) => {
    const blob = await documentsApi.download(documentId);
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  };

  if (detailQuery.isError) {
    return (
      <Stack p="lg">
        <Alert color="red" title="Could not load assignment" styles={dangerAlertStyles}>
          {detailQuery.error instanceof TrellisApiError
            ? detailQuery.error.message
            : "Something went wrong."}
        </Alert>
      </Stack>
    );
  }

  const assignment = detailQuery.data?.assignment;
  const workItem = detailQuery.data?.workItem;

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Stack gap={0}>
          <Button
            variant="subtle"
            size="compact-sm"
            onClick={() => navigate("/vendor")}
            w="fit-content"
          >
            ← Assignments
          </Button>
          <Title order={2}>{detailQuery.data?.tenantName}</Title>
        </Stack>
        {assignment?.status && (
          <Badge color={assignmentStatusColor[assignment.status] ?? "gray.6"}>
            {assignment.status}
          </Badge>
        )}
      </Group>

      {(startMutation.isError || completeMutation.isError) && (
        <Alert color="red" title="Could not update status" styles={dangerAlertStyles}>
          {startMutation.error instanceof TrellisApiError
            ? startMutation.error.message
            : completeMutation.error instanceof TrellisApiError
              ? completeMutation.error.message
              : "Something went wrong."}
        </Alert>
      )}

      <Group gap="xs">
        {assignment?.status === "ASSIGNED" && (
          <Button loading={startMutation.isPending} onClick={() => startMutation.mutate()}>
            Start work
          </Button>
        )}
        {assignment?.status === "IN_PROGRESS" && (
          <Button loading={completeMutation.isPending} onClick={() => completeMutation.mutate()}>
            Complete
          </Button>
        )}
      </Group>

      <Stack
        gap="xs"
        p="md"
        style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
      >
        <Text fw={600}>Work item</Text>
        <Text c="gray.7" size="sm">
          Status: {workItem?.status ?? "—"} · Step {workItem?.currentStepOrder ?? "—"}
        </Text>
      </Stack>

      <Stack gap="md">
        <Title order={3}>Evidence</Title>

        {uploadMutation.isError && (
          <Alert color="red" title="Upload failed" styles={dangerAlertStyles}>
            {uploadMutation.error instanceof TrellisApiError
              ? uploadMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>File name</Table.Th>
              <Table.Th>Type</Table.Th>
              <Table.Th>Scan status</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {evidenceQuery.data?.items?.map((doc) => (
              <Table.Tr key={doc.documentId}>
                <Table.Td>{doc.currentVersion?.fileName ?? "—"}</Table.Td>
                <Table.Td>{doc.documentType}</Table.Td>
                <Table.Td>{doc.currentVersion?.scanStatus ?? "—"}</Table.Td>
                <Table.Td>
                  <Button
                    size="xs"
                    variant="subtle"
                    disabled={doc.currentVersion?.scanStatus !== "CLEAN"}
                    onClick={() =>
                      doc.documentId &&
                      doc.currentVersion?.fileName &&
                      handleDownload(doc.documentId, doc.currentVersion.fileName)
                    }
                  >
                    Download
                  </Button>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>

        {evidenceQuery.isSuccess && evidenceQuery.data.items?.length === 0 && (
          <Text c="gray.7">No evidence uploaded yet.</Text>
        )}

        <Group align="flex-end" gap="md">
          <Select
            label="Document type"
            data={documentTypeOptions}
            value={documentType}
            onChange={setDocumentType}
            w={200}
          />
          <Dropzone
            onDrop={(files) => setFile(files[0] ?? null)}
            maxFiles={1}
            accept={{
              "application/pdf": [".pdf"],
              "image/jpeg": [".jpg", ".jpeg"],
              "image/png": [".png"],
            }}
            inputProps={{ "aria-label": "Evidence file" }}
            style={{ flex: 1 }}
          >
            <Text ta="center">{file ? file.name : "Drag a file here, or click to select one"}</Text>
          </Dropzone>
          <Button
            onClick={() => uploadMutation.mutate()}
            loading={uploadMutation.isPending}
            disabled={!file}
          >
            Upload
          </Button>
        </Group>
      </Stack>
    </Stack>
  );
}
