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
import { ownersApi } from "../../../api/owners";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const ownerSchema = z.object({
  name: z.string().min(1, "Owner name is required."),
  email: z.string().email("Enter a valid email address.").or(z.literal("")).optional(),
  phone: z.string().optional(),
});

type OwnerFormValues = z.infer<typeof ownerSchema>;

const statusColor: Record<string, string> = { ACTIVE: "success.8", ARCHIVED: "gray.8" };

/** BDR-OWN-001 — the first step of the exit-criterion flow (roadmap.md's Phase 2 row): a Tenant Admin creates an owner before a property can reference one. */
export function OwnersListPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const ownersQuery = useQuery({
    queryKey: ["propertyasset", "owners"],
    queryFn: () => ownersApi.list(),
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<OwnerFormValues>({ resolver: zodResolver(ownerSchema) });

  const createMutation = useMutation({
    mutationFn: (values: OwnerFormValues) =>
      ownersApi.create({
        name: values.name,
        email: values.email || null,
        phone: values.phone || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["propertyasset", "owners"] });
      reset();
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: ownersApi.archive,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["propertyasset", "owners"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Owners</Title>
        <Button onClick={openModal}>Add owner</Button>
      </Group>

      {ownersQuery.isError && (
        <Alert color="red" title="Could not load owners" styles={dangerAlertStyles}>
          {ownersQuery.error instanceof TrellisApiError
            ? ownersQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive owner" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "This owner may still have an active property."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Email</Table.Th>
            <Table.Th>Phone</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {ownersQuery.data?.items?.map((owner) => (
            <Table.Tr key={owner.ownerId}>
              <Table.Td>{owner.name}</Table.Td>
              <Table.Td>{owner.email ?? "—"}</Table.Td>
              <Table.Td>{owner.phone ?? "—"}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[owner.status ?? ""] ?? "gray.8"}>{owner.status}</Badge>
              </Table.Td>
              <Table.Td>
                {owner.status !== "ARCHIVED" && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      archiveMutation.isPending && archiveMutation.variables === owner.ownerId
                    }
                    onClick={() => {
                      if (owner.ownerId && confirm(`Archive ${owner.name}?`)) {
                        archiveMutation.mutate(owner.ownerId);
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

      {ownersQuery.isSuccess && ownersQuery.data.items?.length === 0 && (
        <Text c="gray.7">No owners yet. Add the first one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Add an owner">
        {createMutation.isError && (
          <Alert color="red" title="Could not add owner" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput label="Name" error={errors.name?.message} {...register("name")} />
            <TextInput label="Email" error={errors.email?.message} {...register("email")} />
            <TextInput label="Phone" error={errors.phone?.message} {...register("phone")} />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Add owner
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
