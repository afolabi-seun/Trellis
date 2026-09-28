import { Alert, Button, Group, Stack, Text, Title } from "@mantine/core";
import { useMutation, useQuery } from "@tanstack/react-query";
import { reportingApi } from "../../../api/reporting";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles } from "../../../theme/dangerStyles";

/** BDR-RPT-001 — a tenant-wide operational summary (no dollar figures), reachable by both TENANT_ADMIN and DEPARTMENT_STAFF. Live-computed on every request, exact PlatformBillingPage.tsx stat-tile pattern. */
export function ReportingDashboardPage() {
  const summaryQuery = useQuery({
    queryKey: ["reporting", "dashboard"],
    queryFn: () => reportingApi.getDashboardSummary(),
  });

  const exportMutation = useMutation({
    mutationFn: async () => {
      const blob = await reportingApi.downloadDashboardCsv();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = "dashboard-summary.csv";
      link.click();
      URL.revokeObjectURL(url);
    },
  });

  const summary = summaryQuery.data;

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Reporting</Title>
        <Button
          variant="light"
          loading={exportMutation.isPending}
          onClick={() => exportMutation.mutate()}
        >
          Export CSV
        </Button>
      </Group>

      {summaryQuery.isError && (
        <Alert color="red" title="Could not load dashboard" styles={dangerAlertStyles}>
          {summaryQuery.error instanceof TrellisApiError
            ? summaryQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Group
        gap="xl"
        p="md"
        style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
      >
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Open work orders
          </Text>
          <Text fw={700} size="xl">
            {summary?.openWorkOrders ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Expiring documents
          </Text>
          <Text fw={700} size="xl">
            {summary?.expiringDocuments ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Upcoming inspections
          </Text>
          <Text fw={700} size="xl">
            {summary?.upcomingInspections ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Bottlenecked steps
          </Text>
          <Text fw={700} size="xl">
            {summary?.bottleneckedSteps ?? "—"}
          </Text>
        </Stack>
      </Group>
    </Stack>
  );
}
