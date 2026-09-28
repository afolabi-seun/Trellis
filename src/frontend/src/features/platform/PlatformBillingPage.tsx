import { Alert, Badge, Button, Group, Stack, Table, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { authApi } from "../../api/auth";
import { platformBillingApi } from "../../api/billing";
import { TrellisApiError } from "../../api/client";
import { useAuth } from "../../auth/useAuth";
import { dangerAlertStyles } from "../../theme/dangerStyles";
import { formatCurrency } from "../../utils/formatCurrency";

const subscriptionStatusColor: Record<string, string> = {
  TRIALING: "blue.8",
  ACTIVE: "success.8",
  PAST_DUE: "red.8",
  SUSPENDED: "red.8",
  CANCELED: "gray.8",
};

const invoiceStatusColor: Record<string, string> = {
  DRAFT: "gray.8",
  OPEN: "blue.8",
  PAID: "success.8",
  FAILED: "red.8",
  VOID: "gray.8",
};

/** BDR-BIL-007 — a Super Admin's platform-wide billing and revenue status across every tenant. */
export function PlatformBillingPage() {
  const navigate = useNavigate();
  const { claims, logout } = useAuth();

  const handleLogout = async () => {
    try {
      await authApi.logout();
    } finally {
      logout();
      navigate("/login", { replace: true });
    }
  };

  const summaryQuery = useQuery({
    queryKey: ["platform", "billing", "summary"],
    queryFn: () => platformBillingApi.getSummary(),
  });

  const subscriptionsQuery = useQuery({
    queryKey: ["platform", "billing", "subscriptions"],
    queryFn: () => platformBillingApi.listSubscriptions(),
  });

  const invoicesQuery = useQuery({
    queryKey: ["platform", "billing", "invoices"],
    queryFn: () => platformBillingApi.listInvoices(),
  });

  const summary = summaryQuery.data;

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Group gap="lg">
          <Title order={2}>Billing</Title>
          <Text
            component="button"
            onClick={() => navigate("/platform/tenants")}
            c="blue.9"
            size="sm"
          >
            ← Tenants
          </Text>
        </Group>
        <Group>
          <Text c="gray.7" size="sm">
            {claims?.email}
          </Text>
          <Button variant="subtle" onClick={handleLogout}>
            Sign out
          </Button>
        </Group>
      </Group>

      {(summaryQuery.isError || subscriptionsQuery.isError || invoicesQuery.isError) && (
        <Alert color="red" title="Could not load billing data" styles={dangerAlertStyles}>
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
            MRR
          </Text>
          <Text fw={700} size="xl">
            {summary ? formatCurrency(summary.monthlyRecurringRevenue ?? 0) : "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Trialing
          </Text>
          <Text fw={700} size="xl">
            {summary?.trialingCount ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Active
          </Text>
          <Text fw={700} size="xl">
            {summary?.activeCount ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Past due
          </Text>
          <Text fw={700} size="xl">
            {summary?.pastDueCount ?? "—"}
          </Text>
        </Stack>
        <Stack gap={0}>
          <Text c="gray.7" size="sm">
            Suspended
          </Text>
          <Text fw={700} size="xl">
            {summary?.suspendedCount ?? "—"}
          </Text>
        </Stack>
      </Group>

      <Stack gap="md">
        <Title order={3}>Subscriptions</Title>
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Tenant</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Currency</Table.Th>
              <Table.Th>Created</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {subscriptionsQuery.data?.items?.map((subscription) => (
              <Table.Tr key={subscription.subscriptionId}>
                <Table.Td>{subscription.tenantId}</Table.Td>
                <Table.Td>
                  <Badge color={subscriptionStatusColor[subscription.status ?? ""] ?? "gray.8"}>
                    {subscription.status}
                  </Badge>
                </Table.Td>
                <Table.Td>{subscription.currency}</Table.Td>
                <Table.Td>
                  {subscription.createdAt
                    ? new Date(subscription.createdAt).toLocaleDateString()
                    : "—"}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        {subscriptionsQuery.isSuccess && subscriptionsQuery.data.items?.length === 0 && (
          <Text c="gray.7">No subscriptions yet.</Text>
        )}
      </Stack>

      <Stack gap="md">
        <Title order={3}>Invoices</Title>
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Tenant</Table.Th>
              <Table.Th>Period</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Total</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {invoicesQuery.data?.items?.map((invoice) => (
              <Table.Tr key={invoice.invoiceId}>
                <Table.Td>{invoice.tenantId}</Table.Td>
                <Table.Td>
                  {invoice.billingPeriodStart} – {invoice.billingPeriodEnd}
                </Table.Td>
                <Table.Td>
                  <Badge color={invoiceStatusColor[invoice.status ?? ""] ?? "gray.8"}>
                    {invoice.status}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  {formatCurrency(invoice.totalAmount ?? 0, invoice.currency ?? "USD")}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        {invoicesQuery.isSuccess && invoicesQuery.data.items?.length === 0 && (
          <Text c="gray.7">No invoices yet.</Text>
        )}
      </Stack>
    </Stack>
  );
}
