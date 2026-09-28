import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Button,
  CopyButton,
  Group,
  Modal,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { ownersApi } from "../../../api/owners";
import { ownerReportsApi } from "../../../api/reporting";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

const reportSchema = z.object({
  ownerId: z.string().min(1, "An owner is required."),
  periodType: z.string().min(1, "A period type is required."),
  periodStart: z.string().min(1, "A start date is required."),
  periodEnd: z.string().min(1, "An end date is required."),
});

type ReportFormValues = z.infer<typeof reportSchema>;

const statusColor: Record<string, string> = {
  GENERATING: "blue.8",
  READY: "success.8",
  FAILED: "red.8",
};

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/** BDR-OWN-002/003/004 — generate, export, email, and share-link an Owner report. TENANT_ADMIN only, same financial-sensitivity class as Billing's own settings page. */
export function OwnerReportsPage() {
  const queryClient = useQueryClient();
  const [shareLinkResult, setShareLinkResult] = useState<{
    reportShareLinkId: string;
    token: string;
  } | null>(null);

  const ownersQuery = useQuery({
    queryKey: ["propertyasset", "owners"],
    queryFn: () => ownersApi.list(),
  });
  const reportsQuery = useQuery({
    queryKey: ["reporting", "owner-reports"],
    queryFn: () => ownerReportsApi.list(),
  });

  const ownerOptions =
    ownersQuery.data?.items?.map((o) => ({ value: o.ownerId!, label: o.name! })) ?? [];
  const ownerEmailById = new Map(ownersQuery.data?.items?.map((o) => [o.ownerId, o.email]) ?? []);

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ReportFormValues>({
    resolver: zodResolver(reportSchema),
    defaultValues: { periodType: "MONTHLY" },
  });

  const generateMutation = useMutation({
    mutationFn: (values: ReportFormValues) => ownerReportsApi.generate(values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["reporting", "owner-reports"] });
      reset({ periodType: "MONTHLY", ownerId: "", periodStart: "", periodEnd: "" });
    },
  });

  const downloadPdfMutation = useMutation({
    mutationFn: async (ownerReportId: string) =>
      downloadBlob(
        await ownerReportsApi.downloadPdf(ownerReportId),
        `owner-report-${ownerReportId}.pdf`,
      ),
  });

  const downloadCsvMutation = useMutation({
    mutationFn: async (ownerReportId: string) =>
      downloadBlob(
        await ownerReportsApi.downloadCsv(ownerReportId),
        `owner-report-${ownerReportId}.csv`,
      ),
  });

  const deliverMutation = useMutation({
    mutationFn: (ownerReportId: string) => ownerReportsApi.deliver(ownerReportId),
  });

  const createShareLinkMutation = useMutation({
    mutationFn: (ownerReportId: string) => ownerReportsApi.createShareLink(ownerReportId),
    onSuccess: (result) =>
      setShareLinkResult({ reportShareLinkId: result.reportShareLinkId!, token: result.token! }),
  });

  const revokeShareLinkMutation = useMutation({
    mutationFn: (reportShareLinkId: string) => ownerReportsApi.revokeShareLink(reportShareLinkId),
    onSuccess: () => setShareLinkResult(null),
  });

  const shareUrl = shareLinkResult ? `${API_BASE_URL}/reports/shared/${shareLinkResult.token}` : "";

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Owner reports</Title>

      {generateMutation.isError && (
        <Alert color="red" title="Could not generate report" styles={dangerAlertStyles}>
          {generateMutation.error instanceof TrellisApiError
            ? generateMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {reportsQuery.isError && (
        <Alert color="red" title="Could not load owner reports" styles={dangerAlertStyles}>
          {reportsQuery.error instanceof TrellisApiError
            ? reportsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <form onSubmit={handleSubmit((values) => generateMutation.mutate(values))}>
        <Group align="flex-end" gap="md">
          <Controller
            control={control}
            name="ownerId"
            render={({ field }) => (
              <Select
                label="Owner"
                placeholder="Select an owner"
                data={ownerOptions}
                value={field.value}
                onChange={(value) => field.onChange(value ?? "")}
                error={errors.ownerId?.message}
              />
            )}
          />
          <Controller
            control={control}
            name="periodType"
            render={({ field }) => (
              <Select
                label="Period"
                data={[
                  { value: "MONTHLY", label: "Monthly" },
                  { value: "QUARTERLY", label: "Quarterly" },
                ]}
                value={field.value}
                onChange={(value) => field.onChange(value ?? "MONTHLY")}
              />
            )}
          />
          <Controller
            control={control}
            name="periodStart"
            render={({ field }) => (
              <TextInput
                label="Start"
                type="date"
                error={errors.periodStart?.message}
                value={field.value ?? ""}
                onChange={field.onChange}
              />
            )}
          />
          <Controller
            control={control}
            name="periodEnd"
            render={({ field }) => (
              <TextInput
                label="End"
                type="date"
                error={errors.periodEnd?.message}
                value={field.value ?? ""}
                onChange={field.onChange}
              />
            )}
          />
          <Button type="submit" loading={generateMutation.isPending}>
            Generate report
          </Button>
        </Group>
      </form>

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Owner</Table.Th>
            <Table.Th>Period</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Generated</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {reportsQuery.data?.items?.map((report) => {
            const ownerHasEmail = Boolean(ownerEmailById.get(report.ownerId ?? ""));
            const isReady = report.status === "READY";
            return (
              <Table.Tr key={report.ownerReportId}>
                <Table.Td>{report.ownerName}</Table.Td>
                <Table.Td>
                  {report.periodStart} – {report.periodEnd}
                </Table.Td>
                <Table.Td>
                  <Badge color={statusColor[report.status ?? ""] ?? "gray.8"}>
                    {report.status}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  {report.createdAt ? new Date(report.createdAt).toLocaleDateString() : "—"}
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <Button
                      size="xs"
                      variant="subtle"
                      disabled={!isReady}
                      loading={
                        downloadPdfMutation.isPending &&
                        downloadPdfMutation.variables === report.ownerReportId
                      }
                      onClick={() => downloadPdfMutation.mutate(report.ownerReportId!)}
                    >
                      PDF
                    </Button>
                    <Button
                      size="xs"
                      variant="subtle"
                      disabled={!isReady}
                      loading={
                        downloadCsvMutation.isPending &&
                        downloadCsvMutation.variables === report.ownerReportId
                      }
                      onClick={() => downloadCsvMutation.mutate(report.ownerReportId!)}
                    >
                      CSV
                    </Button>
                    <Button
                      size="xs"
                      variant="subtle"
                      disabled={!isReady || !ownerHasEmail}
                      title={ownerHasEmail ? undefined : "This owner has no email on file"}
                      loading={
                        deliverMutation.isPending &&
                        deliverMutation.variables === report.ownerReportId
                      }
                      onClick={() => deliverMutation.mutate(report.ownerReportId!)}
                    >
                      Email to owner
                    </Button>
                    <Button
                      size="xs"
                      variant="subtle"
                      disabled={!isReady}
                      loading={
                        createShareLinkMutation.isPending &&
                        createShareLinkMutation.variables === report.ownerReportId
                      }
                      onClick={() => createShareLinkMutation.mutate(report.ownerReportId!)}
                    >
                      Share link
                    </Button>
                  </Group>
                </Table.Td>
              </Table.Tr>
            );
          })}
        </Table.Tbody>
      </Table>

      {reportsQuery.isSuccess && reportsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No reports generated yet.</Text>
      )}

      <Modal
        opened={shareLinkResult !== null}
        onClose={() => setShareLinkResult(null)}
        title="Share link created"
      >
        <Stack gap="md">
          <Text size="sm" c="gray.7">
            This link won&apos;t be shown again — copy it now if you need to send it manually.
          </Text>
          <Group gap="xs">
            <TextInput value={shareUrl} readOnly style={{ flex: 1 }} />
            <CopyButton value={shareUrl}>
              {({ copied, copy }) => (
                <Button onClick={copy} variant="light">
                  {copied ? "Copied" : "Copy"}
                </Button>
              )}
            </CopyButton>
          </Group>
          <Button
            variant="subtle"
            styles={dangerButtonStyles}
            loading={revokeShareLinkMutation.isPending}
            onClick={() => revokeShareLinkMutation.mutate(shareLinkResult!.reportShareLinkId)}
          >
            Revoke this link
          </Button>
        </Stack>
      </Modal>
    </Stack>
  );
}
