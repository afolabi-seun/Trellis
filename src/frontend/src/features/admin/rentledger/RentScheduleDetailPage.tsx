import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Anchor,
  Badge,
  Button,
  Group,
  Modal,
  NumberInput,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { Link, useParams } from "react-router-dom";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { rentSchedulesApi } from "../../../api/rentledger";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";
import { RentDisclaimer, RentStatusBadge } from "./rentStatus";

const today = () => new Date().toISOString().slice(0, 10);

const paymentSchema = z.object({
  amountReceived: z
    .number({ invalid_type_error: "Amount is required." })
    .positive("Amount must be positive."),
  dateReceived: z
    .string()
    .min(1, "Date received is required.")
    .refine((d) => d <= today(), "Date received can't be in the future."),
  methodReference: z.string().max(255, "Keep this under 255 characters.").optional(),
});
type PaymentFormValues = z.infer<typeof paymentSchema>;

const termsSchema = z.object({
  rentAmount: z
    .number({ invalid_type_error: "Rent is required." })
    .positive("Rent must be positive."),
  dueDayOfMonth: z
    .number({ invalid_type_error: "Due day is required." })
    .int()
    .min(1, "Between 1 and 31.")
    .max(31, "Between 1 and 31."),
  gracePeriodDays: z
    .number({ invalid_type_error: "Grace period is required." })
    .int()
    .min(0, "Between 0 and 60.")
    .max(60, "Between 0 and 60."),
});
type TermsFormValues = z.infer<typeof termsSchema>;

function messageFor(error: unknown): string {
  if (error instanceof TrellisApiError) {
    if (error.code === "TRL-E-RENT-12002")
      return "This schedule has ended, so it can't be changed.";
    if (error.code === "TRL-E-RENT-12000") return "That has already been done.";
    return error.message;
  }
  return "Something went wrong.";
}

/** BDR-RENT-002 — one schedule's terms and the payments recorded against it. Every payment here is a record of something received outside Trellis; amounts, dates and references are never edited — a mistaken entry is voided. */
export function RentScheduleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [paymentOpened, { open: openPayment, close: closePayment }] = useDisclosure(false);
  const [termsOpened, { open: openTerms, close: closeTerms }] = useDisclosure(false);
  const [voidingId, setVoidingId] = useState<string | null>(null);
  const [voidReason, setVoidReason] = useState("");

  const scheduleQuery = useQuery({
    queryKey: ["rent", "schedule", id],
    queryFn: () => rentSchedulesApi.getById(id!),
    enabled: Boolean(id),
  });
  const paymentsQuery = useQuery({
    queryKey: ["rent", "payments", id],
    queryFn: () => rentSchedulesApi.listPaymentRecords(id!),
    enabled: Boolean(id),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["rent"] });

  const paymentForm = useForm<PaymentFormValues>({
    resolver: zodResolver(paymentSchema),
    defaultValues: { dateReceived: today() },
  });
  const termsForm = useForm<TermsFormValues>({ resolver: zodResolver(termsSchema) });

  const recordMutation = useMutation({
    mutationFn: (values: PaymentFormValues) =>
      rentSchedulesApi.recordPaymentReceived(id!, {
        amountReceived: values.amountReceived,
        dateReceived: values.dateReceived,
        methodReference: values.methodReference || null,
      }),
    onSuccess: () => {
      refresh();
      paymentForm.reset({ dateReceived: today() });
      closePayment();
    },
  });

  const updateMutation = useMutation({
    mutationFn: (values: TermsFormValues) =>
      rentSchedulesApi.update(id!, { ...values, rowVersion: scheduleQuery.data!.rowVersion! }),
    onSuccess: () => {
      refresh();
      closeTerms();
    },
  });

  const endMutation = useMutation({
    mutationFn: () => rentSchedulesApi.end(id!),
    onSuccess: refresh,
  });

  const voidMutation = useMutation({
    mutationFn: ({ recordId, reason }: { recordId: string; reason: string }) =>
      rentSchedulesApi.voidPaymentRecord(id!, recordId, reason),
    onSuccess: () => {
      refresh();
      setVoidingId(null);
      setVoidReason("");
    },
  });

  const schedule = scheduleQuery.data;
  const isActive = schedule?.status === "ACTIVE";
  const currency = schedule?.currency ?? "USD";

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>{schedule ? `Rent — ${schedule.unitLabel}` : "Rent schedule"}</Title>
        <Group>
          <Anchor component={Link} to="/admin/rent-schedules">
            All schedules
          </Anchor>
          <Anchor component={Link} to="/admin/rent-arrears">
            Arrears
          </Anchor>
        </Group>
      </Group>

      <RentDisclaimer />

      {(scheduleQuery.isError || endMutation.isError) && (
        <Alert color="red" title="Something went wrong" styles={dangerAlertStyles}>
          {messageFor(scheduleQuery.error ?? endMutation.error)}
        </Alert>
      )}

      {schedule && (
        <Stack gap="xs">
          <Group>
            <Text fw={700} size="xl">
              {formatCurrency(schedule.rentAmount ?? 0, currency)} per period
            </Text>
            <RentStatusBadge status={schedule.rentStatus} />
            {!isActive && <Badge color="gray.8">{schedule.status}</Badge>}
          </Group>
          <Text>
            Due on day {schedule.dueDayOfMonth} of each month, from {schedule.startDate}. Grace
            period: {schedule.gracePeriodDays} days.
          </Text>
          <Text>Recorded as received: {formatCurrency(schedule.totalReceived ?? 0, currency)}</Text>
          {isActive && (schedule.periodsInArrears ?? 0) > 0 && (
            <Text>
              {schedule.periodsInArrears} period(s) past grace —{" "}
              {formatCurrency(schedule.amountShort ?? 0, currency)} not yet recorded as received,
              oldest unpaid due date {schedule.oldestUnpaidDueDate}.
            </Text>
          )}
          {isActive && (
            <Group mt="xs">
              <Button onClick={openPayment}>Record payment received</Button>
              <Button
                variant="default"
                onClick={() => {
                  termsForm.reset({
                    rentAmount: schedule.rentAmount,
                    dueDayOfMonth: schedule.dueDayOfMonth,
                    gracePeriodDays: schedule.gracePeriodDays,
                  });
                  openTerms();
                }}
              >
                Edit terms
              </Button>
              <Button
                variant="subtle"
                styles={dangerButtonStyles}
                loading={endMutation.isPending}
                onClick={() => {
                  if (
                    confirm("End this rent schedule? No further rent will be tracked against it.")
                  ) {
                    endMutation.mutate();
                  }
                }}
              >
                End schedule
              </Button>
            </Group>
          )}
        </Stack>
      )}

      <Title order={3}>Payments recorded as received</Title>
      <Table striped>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Date received</Table.Th>
            <Table.Th>Amount</Table.Th>
            <Table.Th>Reference</Table.Th>
            <Table.Th>Recorded by</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {paymentsQuery.data?.map((record) => (
            <Table.Tr key={record.rentPaymentRecordId}>
              <Table.Td>{record.dateReceived}</Table.Td>
              <Table.Td>
                {formatCurrency(record.amountReceived ?? 0, currency)}
                {record.isVoided && (
                  <>
                    {" "}
                    <Badge color="gray.8">Voided</Badge>
                  </>
                )}
              </Table.Td>
              <Table.Td>{record.methodReference}</Table.Td>
              <Table.Td>{record.recordedByUserId?.slice(0, 8)}</Table.Td>
              <Table.Td>
                {!record.isVoided && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    onClick={() => setVoidingId(record.rentPaymentRecordId!)}
                  >
                    Void
                  </Button>
                )}
                {record.isVoided && (
                  <Text size="sm" c="gray.7">
                    {record.voidReason}
                  </Text>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
      {paymentsQuery.isSuccess && paymentsQuery.data.length === 0 && (
        <Text c="gray.7">No payments have been recorded against this schedule yet.</Text>
      )}

      <Modal opened={paymentOpened} onClose={closePayment} title="Record payment received">
        <Text size="sm" mb="md">
          Record a payment that was received outside Trellis (for example a bank transfer or cash).
          Do not enter card or account numbers.
        </Text>
        {recordMutation.isError && (
          <Alert color="red" title="Could not record payment" styles={dangerAlertStyles} mb="md">
            {messageFor(recordMutation.error)}
          </Alert>
        )}
        <form onSubmit={paymentForm.handleSubmit((values) => recordMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={paymentForm.control}
              name="amountReceived"
              render={({ field }) => (
                <NumberInput
                  label="Amount received"
                  min={0}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={paymentForm.formState.errors.amountReceived?.message}
                />
              )}
            />
            <TextInput
              label="Date received"
              type="date"
              max={today()}
              error={paymentForm.formState.errors.dateReceived?.message}
              {...paymentForm.register("dateReceived")}
            />
            <TextInput
              label="Reference (e.g. EFT ref)"
              error={paymentForm.formState.errors.methodReference?.message}
              {...paymentForm.register("methodReference")}
            />
            <Button type="submit" loading={recordMutation.isPending} fullWidth>
              Save record
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal opened={termsOpened} onClose={closeTerms} title="Edit rent terms">
        {updateMutation.isError && (
          <Alert color="red" title="Could not save changes" styles={dangerAlertStyles} mb="md">
            {updateMutation.error instanceof TrellisApiError && updateMutation.error.status === 409
              ? "Someone else changed this schedule. Reload and try again."
              : messageFor(updateMutation.error)}
          </Alert>
        )}
        <form onSubmit={termsForm.handleSubmit((values) => updateMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={termsForm.control}
              name="rentAmount"
              render={({ field }) => (
                <NumberInput
                  label="Rent per period"
                  min={0}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={termsForm.formState.errors.rentAmount?.message}
                />
              )}
            />
            <Controller
              control={termsForm.control}
              name="dueDayOfMonth"
              render={({ field }) => (
                <NumberInput
                  label="Due day of month"
                  min={1}
                  max={31}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={termsForm.formState.errors.dueDayOfMonth?.message}
                />
              )}
            />
            <Controller
              control={termsForm.control}
              name="gracePeriodDays"
              render={({ field }) => (
                <NumberInput
                  label="Grace period (days)"
                  min={0}
                  max={60}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={termsForm.formState.errors.gracePeriodDays?.message}
                />
              )}
            />
            <Button type="submit" loading={updateMutation.isPending} fullWidth>
              Save changes
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal
        opened={voidingId !== null}
        onClose={() => setVoidingId(null)}
        title="Void this record"
      >
        <Text size="sm" mb="md">
          A voided record stays in the history but no longer counts toward what was received.
        </Text>
        {voidMutation.isError && (
          <Alert color="red" title="Could not void record" styles={dangerAlertStyles} mb="md">
            {messageFor(voidMutation.error)}
          </Alert>
        )}
        <Stack gap="md">
          <TextInput
            label="Reason"
            value={voidReason}
            onChange={(event) => setVoidReason(event.currentTarget.value)}
          />
          <Button
            styles={dangerButtonStyles}
            variant="default"
            disabled={voidReason.trim().length === 0}
            loading={voidMutation.isPending}
            onClick={() => voidMutation.mutate({ recordId: voidingId!, reason: voidReason.trim() })}
          >
            Void record
          </Button>
        </Stack>
      </Modal>
    </Stack>
  );
}
