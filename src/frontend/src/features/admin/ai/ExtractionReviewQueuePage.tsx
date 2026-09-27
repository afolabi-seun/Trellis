import { Alert, Badge, Stack, Table, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { aiExtractionApi } from "../../../api/aiExtraction";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles } from "../../../theme/dangerStyles";

const extractionStatusColor: Record<string, string> = {
  PENDING: "yellow.8",
  EXTRACTED: "blue.8",
  MANUAL_REVIEW_REQUIRED: "red.8",
  CONFIRMED: "success.8",
};

/** BDR-AI-001/002 — every Lease/InsuranceCert/Licence upload's extraction result, awaiting a human's review and confirmation before it's authoritative. */
export function ExtractionReviewQueuePage() {
  const jobsQuery = useQuery({
    queryKey: ["ai", "extraction-jobs"],
    queryFn: () => aiExtractionApi.list(),
  });

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Extraction review</Title>

      {jobsQuery.isError && (
        <Alert color="red" title="Could not load extraction jobs" styles={dangerAlertStyles}>
          {jobsQuery.error instanceof TrellisApiError
            ? jobsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Document type</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Confidence</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {jobsQuery.data?.items?.map((job) => (
            <Table.Tr key={job.extractionJobId}>
              <Table.Td>{job.documentType}</Table.Td>
              <Table.Td>
                <Badge color={extractionStatusColor[job.status ?? ""] ?? "gray.6"}>
                  {job.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {job.confidence != null ? `${Math.round(job.confidence * 100)}%` : "—"}
              </Table.Td>
              <Table.Td>
                <Text
                  component={Link}
                  to={`/admin/extraction-jobs/${job.extractionJobId}`}
                  fw={600}
                >
                  Review
                </Text>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {jobsQuery.isSuccess && jobsQuery.data.items?.length === 0 && (
        <Text c="gray.7">
          No extraction jobs yet. Upload a lease, insurance, or licence document.
        </Text>
      )}
    </Stack>
  );
}
