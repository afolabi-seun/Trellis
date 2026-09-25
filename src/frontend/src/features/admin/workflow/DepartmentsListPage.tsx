import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { departmentsApi } from "../../../api/departments";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const departmentSchema = z.object({
  name: z.string().min(1, "Department name is required."),
});

type DepartmentFormValues = z.infer<typeof departmentSchema>;

const statusColor: Record<string, string> = { ACTIVE: "success.8", ARCHIVED: "gray.8" };

/** BDR-DEPT-001 — a Tenant Admin's configurable department catalogue; every workflow step routes to one of these. */
export function DepartmentsListPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const departmentsQuery = useQuery({
    queryKey: ["workflow", "departments"],
    queryFn: () => departmentsApi.list(),
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<DepartmentFormValues>({ resolver: zodResolver(departmentSchema) });

  const createMutation = useMutation({
    mutationFn: (values: DepartmentFormValues) => departmentsApi.create({ name: values.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workflow", "departments"] });
      reset();
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: departmentsApi.archive,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow", "departments"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Departments</Title>
        <Button onClick={openModal}>Add department</Button>
      </Group>

      {departmentsQuery.isError && (
        <Alert color="red" title="Could not load departments" styles={dangerAlertStyles}>
          {departmentsQuery.error instanceof TrellisApiError
            ? departmentsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive department" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {departmentsQuery.data?.items?.map((department) => (
            <Table.Tr key={department.departmentId}>
              <Table.Td>{department.name}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[department.status ?? ""] ?? "gray.8"}>
                  {department.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {department.status !== "ARCHIVED" && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      archiveMutation.isPending &&
                      archiveMutation.variables === department.departmentId
                    }
                    onClick={() => {
                      if (department.departmentId && confirm(`Archive ${department.name}?`)) {
                        archiveMutation.mutate(department.departmentId);
                      }
                    }}
                  >
                    Archive
                  </Button>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {departmentsQuery.isSuccess && departmentsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No departments yet. Add the first one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Add a department">
        {createMutation.isError && (
          <Alert color="red" title="Could not add department" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput label="Name" error={errors.name?.message} {...register("name")} />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Add department
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
