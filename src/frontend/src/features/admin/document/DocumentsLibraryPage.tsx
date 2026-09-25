import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { Dropzone } from "@mantine/dropzone";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { TrellisApiError } from "../../../api/client";
import { documentsApi } from "../../../api/documents";
import { dangerAlertStyles } from "../../../theme/dangerStyles";

const documentTypeOptions = [
  { value: "LEASE", label: "Lease" },
  { value: "INSURANCE_CERT", label: "Insurance certificate" },
  { value: "LICENCE", label: "Licence" },
  { value: "INVOICE", label: "Invoice" },
  { value: "OTHER", label: "Other" },
];

const scanStatusColor: Record<string, string> = {
  PENDING: "yellow.8",
  CLEAN: "success.8",
  INFECTED: "red.8",
  SCAN_FAILED: "red.8",
};

/** roadmap.md's "Document library" — a global list across every tenant document, upload gated by scan_status (security_guide.md's File Upload Security) before it's downloadable by anyone but its uploader. */
export function DocumentsLibraryPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [file, setFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState<string | null>("OTHER");
  const [expiresAt, setExpiresAt] = useState("");

  const documentsQuery = useQuery({
    queryKey: ["document", "documents"],
    queryFn: () => documentsApi.list(),
  });

  const uploadMutation = useMutation({
    mutationFn: () => {
      if (!file || !documentType) {
        throw new Error("A file and document type are required.");
      }
      return documentsApi.create({ documentType, expiresAt: expiresAt || undefined, file });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["document", "documents"] });
      setFile(null);
      setExpiresAt("");
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: documentsApi.archive,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["document", "documents"] }),
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

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Document library</Title>
        <Button onClick={openModal}>Upload document</Button>
      </Group>

      {documentsQuery.isError && (
        <Alert color="red" title="Could not load documents" styles={dangerAlertStyles}>
          {documentsQuery.error instanceof TrellisApiError
            ? documentsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive document" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>File name</Table.Th>
            <Table.Th>Type</Table.Th>
            <Table.Th>Expires</Table.Th>
            <Table.Th>Scan status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {documentsQuery.data?.items?.map((doc) => (
            <Table.Tr key={doc.documentId}>
              <Table.Td>{doc.currentVersion?.fileName ?? "—"}</Table.Td>
              <Table.Td>{doc.documentType}</Table.Td>
              <Table.Td>{doc.expiresAt ?? "—"}</Table.Td>
              <Table.Td>
                <Badge color={scanStatusColor[doc.currentVersion?.scanStatus ?? ""] ?? "gray.6"}>
                  {doc.currentVersion?.scanStatus ?? "—"}
                </Badge>
              </Table.Td>
              <Table.Td>
                <Group gap="xs">
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
                  {doc.status !== "ARCHIVED" && (
                    <Button
                      size="xs"
                      variant="subtle"
                      color="red"
                      loading={
                        archiveMutation.isPending && archiveMutation.variables === doc.documentId
                      }
                      onClick={() =>
                        doc.documentId &&
                        confirm("Archive this document?") &&
                        archiveMutation.mutate(doc.documentId)
                      }
                    >
                      Archive
                    </Button>
                  )}
                </Group>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {documentsQuery.isSuccess && documentsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No documents yet. Upload the first one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Upload a document">
        {uploadMutation.isError && (
          <Alert color="red" title="Upload failed" styles={dangerAlertStyles} mb="md">
            {uploadMutation.error instanceof TrellisApiError
              ? uploadMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <Stack gap="md">
          <Select
            label="Document type"
            data={documentTypeOptions}
            value={documentType}
            onChange={setDocumentType}
          />
          <TextInput
            label="Expires on"
            type="date"
            value={expiresAt}
            onChange={(e) => setExpiresAt(e.currentTarget.value)}
          />

          <Dropzone
            onDrop={(files) => setFile(files[0] ?? null)}
            maxFiles={1}
            accept={{
              "application/pdf": [".pdf"],
              "application/vnd.openxmlformats-officedocument.wordprocessingml.document": [".docx"],
              "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
              "image/jpeg": [".jpg", ".jpeg"],
              "image/png": [".png"],
            }}
          >
            <Text ta="center">{file ? file.name : "Drag a file here, or click to select one"}</Text>
          </Dropzone>

          <Button
            onClick={() => uploadMutation.mutate()}
            loading={uploadMutation.isPending}
            disabled={!file}
            fullWidth
          >
            Upload
          </Button>
        </Stack>
      </Modal>
    </Stack>
  );
}
