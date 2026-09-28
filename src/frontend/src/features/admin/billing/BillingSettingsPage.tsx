import { Alert, Badge, Button, Group, Stack, Table, Text, Title } from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { billingApi } from "../../../api/billing";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";

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

/** BDR-BIL-006 — a Tenant Admin's own billing state: current usage/projected invoice, invoice history, and payment method management. */
export function BillingSettingsPage() {
  const queryClient = useQueryClient();

  const subscriptionQuery = useQuery({
    queryKey: ["billing", "subscription"],
    queryFn: () => billingApi.getSubscription(),
  });

  const invoicesQuery = useQuery({
    queryKey: ["billing", "invoices"],
    queryFn: () => billingApi.listInvoices(),
  });

  const paymentMethodQuery = useQuery({
    queryKey: ["billing", "payment-method"],
    queryFn: () => billingApi.getPaymentMethod(),
    retry: false,
  });

  // Paystack's real hosted Inline widget is deferred alongside PaystackGateway's own real HTTP
  // integration (see that class's doc comment) — against the NoOp stub, initialize's own
  // reference is confirmed directly, with no real card-entry step in between.
  const addCardMutation = useMutation({
    mutationFn: async () => {
      const initialized = await billingApi.initializePaymentMethod();
      return billingApi.confirmPaymentMethod({ reference: initialized.reference ?? "" });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["billing", "payment-method"] }),
  });

  const subscription = subscriptionQuery.data;

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Billing</Title>

      {subscriptionQuery.isError && (
        <Alert color="red" title="Could not load subscription" styles={dangerAlertStyles}>
          {subscriptionQuery.error instanceof TrellisApiError
            ? subscriptionQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {addCardMutation.isError && (
        <Alert color="red" title="Could not add payment method" styles={dangerAlertStyles}>
          {addCardMutation.error instanceof TrellisApiError
            ? addCardMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Stack
        gap="xs"
        p="md"
        style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
      >
        <Group justify="space-between">
          <Title order={3}>Current usage</Title>
          {subscription?.subscription?.status && (
            <Badge color={subscriptionStatusColor[subscription.subscription.status] ?? "gray.8"}>
              {subscription.subscription.status}
            </Badge>
          )}
        </Group>
        <Text c="gray.7" size="sm">
          Active units: {subscription?.latestActiveUnitCount ?? "—"}
        </Text>
        <Text c="gray.7" size="sm">
          Projected next invoice:{" "}
          {subscription?.projectedInvoiceTotal != null
            ? formatCurrency(
                subscription.projectedInvoiceTotal,
                subscription.subscription?.currency ?? "USD",
              )
            : "—"}
        </Text>
      </Stack>

      <Stack
        gap="xs"
        p="md"
        style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
      >
        <Group justify="space-between">
          <Title order={3}>Payment method</Title>
          <Button
            size="xs"
            loading={addCardMutation.isPending}
            onClick={() => addCardMutation.mutate()}
          >
            {paymentMethodQuery.data ? "Replace card" : "Add card"}
          </Button>
        </Group>
        {paymentMethodQuery.data ? (
          <Text c="gray.7" size="sm">
            {paymentMethodQuery.data.cardBrand} ending in {paymentMethodQuery.data.cardLast4}
          </Text>
        ) : (
          <Text c="gray.7" size="sm">
            No payment method on file yet.
          </Text>
        )}
      </Stack>

      <Stack gap="md">
        <Title order={3}>Invoice history</Title>
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Period</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Total</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {invoicesQuery.data?.items?.map((invoice) => (
              <Table.Tr key={invoice.invoiceId}>
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
