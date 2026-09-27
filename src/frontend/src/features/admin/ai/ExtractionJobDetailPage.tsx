import { Alert, Badge, Button, Group, Stack, Table, Text, TextInput, Title } from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { aiExtractionApi } from "../../../api/aiExtraction";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles } from "../../../theme/dangerStyles";

const extractionStatusColor: Record<string, string> = {
  PENDING: "yellow.8",
  EXTRACTED: "blue.8",
  MANUAL_REVIEW_REQUIRED: "red.8",
  CONFIRMED: "success.8",
};

interface EditableField {
  fieldName: string;
  value: string;
}

/** BDR-AI-002's review/confirm surface — an editable copy of the extracted fields; Confirm always submits the full field list (the service diffs it against the original to decide whether to write an override_log entry). */
export function ExtractionJobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [fields, setFields] = useState<EditableField[]>([]);

  const jobQuery = useQuery({
    queryKey: ["ai", "extraction-jobs", id],
    queryFn: () => aiExtractionApi.getById(id!),
    enabled: !!id,
  });

  const job = jobQuery.data;

  useEffect(() => {
    const source = job?.confirmedFields ?? job?.extractedFields;
    if (source) {
      setFields(source.map((f) => ({ fieldName: f.fieldName ?? "", value: f.value ?? "" })));
    }
  }, [job?.extractedFields, job?.confirmedFields]);

  const confirmMutation = useMutation({
    mutationFn: () =>
      aiExtractionApi.confirm(id!, {
        fields: fields.map((f) => ({ fieldName: f.fieldName, value: f.value })),
        rowVersion: job!.rowVersion!,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["ai", "extraction-jobs"] });
      queryClient.invalidateQueries({ queryKey: ["ai", "extraction-jobs", id] });
    },
  });

  const canConfirm = job?.status === "EXTRACTED" || job?.status === "MANUAL_REVIEW_REQUIRED";

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Stack gap={0}>
          <Button
            variant="subtle"
            size="compact-sm"
            onClick={() => navigate("/admin/extraction-jobs")}
            w="fit-content"
          >
            ← Extraction review
          </Button>
          <Title order={2}>{job?.documentType ?? "Extraction job"}</Title>
        </Stack>
        <Group gap="sm">
          {job?.confidence != null && (
            <Badge color="gray.8">{Math.round(job.confidence * 100)}% confidence</Badge>
          )}
          {job?.status && (
            <Badge color={extractionStatusColor[job.status] ?? "gray.6"}>{job.status}</Badge>
          )}
        </Group>
      </Group>

      {jobQuery.isError && (
        <Alert color="red" title="Could not load extraction job" styles={dangerAlertStyles}>
          {jobQuery.error instanceof TrellisApiError
            ? jobQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {confirmMutation.isError && (
        <Alert color="red" title="Could not confirm extraction" styles={dangerAlertStyles}>
          {confirmMutation.error instanceof TrellisApiError
            ? confirmMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {job?.status === "PENDING" && <Text c="gray.7">Extraction is still in progress.</Text>}

      {fields.length > 0 && (
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Field</Table.Th>
              <Table.Th>Value</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {fields.map((field, index) => (
              <Table.Tr key={field.fieldName}>
                <Table.Td>{field.fieldName}</Table.Td>
                <Table.Td>
                  <TextInput
                    aria-label={field.fieldName}
                    value={field.value}
                    disabled={!canConfirm}
                    onChange={(e) =>
                      setFields((prev) =>
                        prev.map((f, i) =>
                          i === index ? { ...f, value: e.currentTarget.value } : f,
                        ),
                      )
                    }
                  />
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      {canConfirm && (
        <Button
          onClick={() => confirmMutation.mutate()}
          loading={confirmMutation.isPending}
          w="fit-content"
        >
          Confirm
        </Button>
      )}
    </Stack>
  );
}
