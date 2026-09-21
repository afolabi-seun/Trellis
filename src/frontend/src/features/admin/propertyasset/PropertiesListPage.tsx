import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Anchor,
  Badge,
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
import { Controller, useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { ownersApi } from "../../../api/owners";
import { propertiesApi } from "../../../api/properties";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const propertySchema = z.object({
  ownerId: z.string().min(1, "Select an owner."),
  address: z.string().min(1, "Address is required."),
  propertyType: z.string().min(1, "Select a property type."),
  sizeSqft: z.number().nonnegative().nullable().optional(),
  acquisitionDate: z.string().optional(),
  acquisitionValue: z.number().nonnegative().nullable().optional(),
  currentValuation: z.number().nonnegative().nullable().optional(),
});

type PropertyFormValues = z.infer<typeof propertySchema>;

const propertyTypeOptions = [
  { value: "RESIDENTIAL", label: "Residential" },
  { value: "COMMERCIAL", label: "Commercial" },
  { value: "MIXED_USE", label: "Mixed use" },
];

const statusColor: Record<string, string> = { ACTIVE: "success.8", ARCHIVED: "gray.8" };

/** BDR-AST-001/002 — the second step of the exit-criterion flow: a property, owned by an already-created owner. */
export function PropertiesListPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const propertiesQuery = useQuery({
    queryKey: ["propertyasset", "properties"],
    queryFn: () => propertiesApi.list(),
  });
  const ownersQuery = useQuery({
    queryKey: ["propertyasset", "owners"],
    queryFn: () => ownersApi.list(),
  });

  const ownerNameById = new Map(ownersQuery.data?.items?.map((o) => [o.ownerId, o.name]) ?? []);

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<PropertyFormValues>({ resolver: zodResolver(propertySchema) });

  const createMutation = useMutation({
    mutationFn: (values: PropertyFormValues) =>
      propertiesApi.create({
        ownerId: values.ownerId,
        address: values.address,
        propertyType: values.propertyType,
        sizeSqft: values.sizeSqft ?? null,
        acquisitionDate: values.acquisitionDate || null,
        acquisitionValue: values.acquisitionValue ?? null,
        currentValuation: values.currentValuation ?? null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["propertyasset", "properties"] });
      reset();
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: propertiesApi.archive,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["propertyasset", "properties"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Properties</Title>
        <Button onClick={openModal} disabled={!ownersQuery.data?.items?.length}>
          Add property
        </Button>
      </Group>

      {ownersQuery.isSuccess && ownersQuery.data.items?.length === 0 && (
        <Text c="gray.7">
          Add an owner first — every property needs one.{" "}
          {/* BDR-NFR-007 (WCAG 2.1 AA) — a link inline in a text block needs either 3:1 contrast
              against the surrounding text or non-color styling to distinguish it; the sage
              accent color against gray.7 body text was only 1.37:1 (axe's link-in-text-block
              rule, found live). underline="always" is the non-color fix. */}
          <Anchor component={Link} to="/admin/owners" underline="always">
            Go to owners
          </Anchor>
        </Text>
      )}

      {propertiesQuery.isError && (
        <Alert color="red" title="Could not load properties" styles={dangerAlertStyles}>
          {propertiesQuery.error instanceof TrellisApiError
            ? propertiesQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive property" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "This property may still have an active unit or asset."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Address</Table.Th>
            <Table.Th>Owner</Table.Th>
            <Table.Th>Type</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {propertiesQuery.data?.items?.map((property) => (
            <Table.Tr key={property.propertyId}>
              <Table.Td>
                <Anchor component={Link} to={`/admin/properties/${property.propertyId}`}>
                  {property.address}
                </Anchor>
              </Table.Td>
              <Table.Td>{ownerNameById.get(property.ownerId ?? "") ?? "—"}</Table.Td>
              <Table.Td>{property.propertyType}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[property.status ?? ""] ?? "gray.8"}>
                  {property.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {property.status !== "ARCHIVED" && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      archiveMutation.isPending && archiveMutation.variables === property.propertyId
                    }
                    onClick={() => {
                      if (property.propertyId && confirm(`Archive ${property.address}?`)) {
                        archiveMutation.mutate(property.propertyId);
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

      {propertiesQuery.isSuccess && propertiesQuery.data.items?.length === 0 && (
        <Text c="gray.7">No properties yet.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Add a property" size="lg">
        {createMutation.isError && (
          <Alert color="red" title="Could not add property" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              name="ownerId"
              control={control}
              defaultValue=""
              render={({ field }) => (
                <Select
                  label="Owner"
                  data={
                    ownersQuery.data?.items?.map((o) => ({
                      value: o.ownerId ?? "",
                      label: o.name ?? "",
                    })) ?? []
                  }
                  error={errors.ownerId?.message}
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? "")}
                />
              )}
            />
            <TextInput label="Address" error={errors.address?.message} {...register("address")} />
            <Controller
              name="propertyType"
              control={control}
              defaultValue=""
              render={({ field }) => (
                <Select
                  label="Property type"
                  data={propertyTypeOptions}
                  error={errors.propertyType?.message}
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? "")}
                />
              )}
            />
            <Controller
              name="sizeSqft"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label="Size (sq ft)"
                  min={0}
                  value={field.value ?? ""}
                  onChange={(v) => field.onChange(v === "" ? null : v)}
                />
              )}
            />
            <TextInput label="Acquisition date" type="date" {...register("acquisitionDate")} />
            <Controller
              name="acquisitionValue"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label="Acquisition value"
                  min={0}
                  prefix="$"
                  thousandSeparator
                  value={field.value ?? ""}
                  onChange={(v) => field.onChange(v === "" ? null : v)}
                />
              )}
            />
            <Controller
              name="currentValuation"
              control={control}
              render={({ field }) => (
                <NumberInput
                  label="Current valuation"
                  min={0}
                  prefix="$"
                  thousandSeparator
                  value={field.value ?? ""}
                  onChange={(v) => field.onChange(v === "" ? null : v)}
                />
              )}
            />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Add property
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
