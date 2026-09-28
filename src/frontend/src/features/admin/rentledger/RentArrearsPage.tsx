import { Alert, Anchor, Stack, Table, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { TrellisApiError } from "../../../api/client";
import { rentSchedulesApi } from "../../../api/rentledger";
import { dangerAlertStyles, infoAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";
import { RentDisclaimer } from "./rentStatus";

/** BDR-RENT-003 — schedules with rent unrecorded past its grace period, oldest first. Computed on read from the recorded payments, so a payment recorded late clears a row here immediately. */
export function RentArrearsPage() {
  const arrearsQuery = useQuery({
    queryKey: ["rent", "arrears"],
    queryFn: () => rentSchedulesApi.listArrears(),
  });

  const isModuleNotEntitled =
    arrearsQuery.error instanceof TrellisApiError && arrearsQuery.error.code === "TRL-E-BIL-10000";
  if (isModuleNotEntitled) {
    return (
      <Stack gap="md" p="lg">
        <Title order={2}>Rent arrears</Title>
        <Alert
          color="blue"
          title="Rent tracking isn't enabled for your plan"
          styles={infoAlertStyles}
        >
          Arrears flags are part of the Rent Collection add-on. Contact your Trellis administrator
          to enable it.
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Rent arrears</Title>
      <RentDisclaimer />

      {arrearsQuery.isError && (
        <Alert color="red" title="Could not load arrears" styles={dangerAlertStyles}>
          {arrearsQuery.error instanceof TrellisApiError
            ? arrearsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Unit</Table.Th>
            <Table.Th>Periods past grace</Table.Th>
            <Table.Th>Not yet recorded as received</Table.Th>
            <Table.Th>Oldest unpaid due date</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {arrearsQuery.data?.map((schedule) => (
            <Table.Tr key={schedule.rentScheduleId}>
              <Table.Td>
                <Anchor component={Link} to={`/admin/rent-schedules/${schedule.rentScheduleId}`}>
                  {schedule.unitLabel}
                </Anchor>
              </Table.Td>
              <Table.Td>{schedule.periodsInArrears}</Table.Td>
              <Table.Td>
                {formatCurrency(schedule.amountShort ?? 0, schedule.currency ?? "USD")}
              </Table.Td>
              <Table.Td>{schedule.oldestUnpaidDueDate}</Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {arrearsQuery.isSuccess && arrearsQuery.data.length === 0 && (
        <Text c="gray.7">No rent schedules are in arrears.</Text>
      )}
    </Stack>
  );
}
