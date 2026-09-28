import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Anchor,
  Button,
  Group,
  Modal,
  NumberInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { propertiesApi } from "../../../api/properties";
import { rentSchedulesApi } from "../../../api/rentledger";
import { unitsApi } from "../../../api/units";
import { dangerAlertStyles, infoAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";
import { RentDisclaimer, RentStatusBadge } from "./rentStatus";

const scheduleSchema = z.object({
  propertyId: z.string().min(1, "Select a property."),
  unitAssetId: z.string().min(1, "Select a unit."),
  rentAmount: z
    .number({ invalid_type_error: "Rent is required." })
    .positive("Rent must be positive."),
  currency: z.string().length(3, "Use a 3-letter currency code."),
  startDate: z.string().min(1, "Start date is required."),
  dueDayOfMonth: z
    .number({ invalid_type_error: "Due day is required." })
    .int()
    .min(1, "Between 1 and 31.")
    .max(31, "Between 1 and 31."),
  gracePeriodDays: z
    .number()
    .int()
    .min(0, "Between 0 and 60.")
    .max(60, "Between 0 and 60.")
    .optional(),
});

type ScheduleFormValues = z.infer<typeof scheduleSchema>;

/** BDR-RENT-001 — the schedule of rent expected per unit. Lease-created schedules appear here automatically; this also lets staff enter one for a unit occupied before Leasing existed. */
export function RentSchedulesPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const schedulesQuery = useQuery({
    queryKey: ["rent", "schedules"],
    queryFn: () => rentSchedulesApi.list(),
  });
  const propertiesQuery = useQuery({
    queryKey: ["propertyasset", "properties"],
    queryFn: () => propertiesApi.list(),
  });

  const {
    control,
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ScheduleFormValues>({
    resolver: zodResolver(scheduleSchema),
    defaultValues: { currency: "USD", dueDayOfMonth: 1, gracePeriodDays: 5 },
  });

  const selectedPropertyId = useWatch({ control, name: "propertyId" });
  const unitsQuery = useQuery({
    queryKey: ["propertyasset", "units", selectedPropertyId],
    queryFn: () => unitsApi.listByProperty(selectedPropertyId),
    enabled: Boolean(selectedPropertyId),
  });

  const createMutation = useMutation({
    mutationFn: (values: ScheduleFormValues) =>
      rentSchedulesApi.create({
        unitAssetId: values.unitAssetId,
        rentAmount: values.rentAmount,
        currency: values.currency,
        startDate: values.startDate,
        dueDayOfMonth: values.dueDayOfMonth,
        gracePeriodDays: values.gracePeriodDays ?? null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["rent"] });
      reset({ currency: "USD", dueDayOfMonth: 1, gracePeriodDays: 5 });
      closeModal();
    },
  });

  const isModuleNotEntitled =
    schedulesQuery.error instanceof TrellisApiError &&
    schedulesQuery.error.code === "TRL-E-BIL-10000";
  if (isModuleNotEntitled) {
    return (
      <Stack gap="md" p="lg">
        <Title order={2}>Rent schedules</Title>
        <Alert
          color="blue"
          title="Rent tracking isn't enabled for your plan"
          styles={infoAlertStyles}
        >
          Rent schedules, recorded payments and arrears flags are an add-on module. Contact your
          Trellis administrator to enable Rent Collection for your account.
        </Alert>
      </Stack>
    );
  }

  const propertyOptions =
    propertiesQuery.data?.items
      ?.filter((p) => p.status === "ACTIVE")
      .map((p) => ({ value: p.propertyId!, label: p.address! })) ?? [];
  const unitOptions =
    unitsQuery.data?.items
      ?.filter((u) => u.status === "ACTIVE")
      .map((u) => ({ value: u.unitAssetId!, label: u.label! })) ?? [];

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Rent schedules</Title>
        <Group>
          <Anchor component={Link} to="/admin/rent-arrears">
            Arrears
          </Anchor>
          <Button onClick={openModal}>New schedule</Button>
        </Group>
      </Group>

      <RentDisclaimer />

      {schedulesQuery.isError && (
        <Alert color="red" title="Could not load rent schedules" styles={dangerAlertStyles}>
          {schedulesQuery.error instanceof TrellisApiError
            ? schedulesQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Unit</Table.Th>
            <Table.Th>Rent per period</Table.Th>
            <Table.Th>Due day</Table.Th>
            <Table.Th>Recorded as received</Table.Th>
            <Table.Th>Status</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {schedulesQuery.data?.items?.map((schedule) => (
            <Table.Tr key={schedule.rentScheduleId}>
              <Table.Td>
                <Anchor component={Link} to={`/admin/rent-schedules/${schedule.rentScheduleId}`}>
                  {schedule.unitLabel}
                </Anchor>
              </Table.Td>
              <Table.Td>
                {formatCurrency(schedule.rentAmount ?? 0, schedule.currency ?? "USD")}
              </Table.Td>
              <Table.Td>{schedule.dueDayOfMonth}</Table.Td>
              <Table.Td>
                {formatCurrency(schedule.totalReceived ?? 0, schedule.currency ?? "USD")}
              </Table.Td>
              <Table.Td>
                <RentStatusBadge status={schedule.rentStatus} />
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {schedulesQuery.isSuccess && schedulesQuery.data.items?.length === 0 && (
        <Text c="gray.7">
          No rent schedules yet. One is created automatically when a lease is signed, or add one for
          a unit that was already occupied.
        </Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="New rent schedule">
        {createMutation.isError && (
          <Alert color="red" title="Could not create schedule" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError &&
            createMutation.error.code === "TRL-E-RENT-12001"
              ? "That unit already has an active rent schedule."
              : createMutation.error instanceof TrellisApiError
                ? createMutation.error.message
                : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={control}
              name="propertyId"
              render={({ field }) => (
                <Select
                  label="Property"
                  data={propertyOptions}
                  value={field.value ?? null}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.propertyId?.message}
                />
              )}
            />
            <Controller
              control={control}
              name="unitAssetId"
              render={({ field }) => (
                <Select
                  label="Unit"
                  data={unitOptions}
                  value={field.value ?? null}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.unitAssetId?.message}
                  disabled={!selectedPropertyId}
                />
              )}
            />
            <Controller
              control={control}
              name="rentAmount"
              render={({ field }) => (
                <NumberInput
                  label="Rent per period"
                  min={0}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={errors.rentAmount?.message}
                />
              )}
            />
            <TextInput
              label="Currency"
              error={errors.currency?.message}
              {...register("currency")}
            />
            <TextInput
              label="Start date"
              type="date"
              error={errors.startDate?.message}
              {...register("startDate")}
            />
            <Controller
              control={control}
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
                  error={errors.dueDayOfMonth?.message}
                />
              )}
            />
            <Controller
              control={control}
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
                  error={errors.gracePeriodDays?.message}
                />
              )}
            />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Create schedule
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
