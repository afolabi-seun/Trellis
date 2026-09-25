import { Badge, Stack, Table, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { vendorAssignmentsApi } from "../../api/vendorAssignments";

const assignmentStatusColor: Record<string, string> = {
  ASSIGNED: "gray.8",
  IN_PROGRESS: "blue.8",
  COMPLETED: "success.8",
  CLOSED: "gray.8",
};

/** Exit criterion: one Vendor login, assignments across every tenant that engaged it — tenant name resolved server-side, never from a claim. */
export function VendorAssignmentsListPage() {
  const assignmentsQuery = useQuery({
    queryKey: ["vendor-portal", "assignments"],
    queryFn: () => vendorAssignmentsApi.listMine(),
  });

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Assignments</Title>

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Tenant</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Assigned</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {assignmentsQuery.data?.items?.map((item) => (
            <Table.Tr key={item.assignment?.assignmentId}>
              <Table.Td>
                <Text
                  component={Link}
                  to={`/vendor/assignments/${item.assignment?.workItemId}`}
                  fw={600}
                >
                  {item.tenantName}
                </Text>
              </Table.Td>
              <Table.Td>
                <Badge color={assignmentStatusColor[item.assignment?.status ?? ""] ?? "gray.6"}>
                  {item.assignment?.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {item.assignment?.assignedAt
                  ? new Date(item.assignment.assignedAt).toLocaleDateString()
                  : "—"}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {assignmentsQuery.isSuccess && assignmentsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No assignments yet.</Text>
      )}
    </Stack>
  );
}
