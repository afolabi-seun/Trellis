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
import { Controller, useForm } from "react-hook-form";
import { Link, useParams } from "react-router-dom";
import { z } from "zod";
import { propertiesApi } from "../../../api/properties";
import { unitsApi } from "../../../api/units";
import { TrellisApiError } from "../../../api/client";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const unitSchema = z.object({
  label: z.string().min(1, "Label is required."),
  sizeSqft: z.number().nonnegative().nullable().optional(),
  acquisitionValue: z.number().nonnegative().nullable().optional(),
});

type UnitFormValues = z.infer<typeof unitSchema>;

const statusColor: Record<string, string> = { ACTIVE: "success.8", ARCHIVED: "gray.8" };

/** The third step of the exit-criterion flow (roadmap.md's Phase 2 row) — units/assets are managed from within their parent property, matching BDR-AST-001's Owner -> Property -> Unit/Asset hierarchy. */
export function PropertyDetailPage() {
  const { propertyId } = useParams<{ propertyId: string }>();
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const propertyQuery = useQuery({
    queryKey: ["propertyasset", "properties", propertyId],
    queryFn: () => propertiesApi.getById(propertyId!),
    enabled: !!propertyId,
  });

  const unitsQuery = useQuery({
    queryKey: ["propertyasset", "properties", propertyId, "units"],
    queryFn: () => unitsApi.listByProperty(propertyId!),
    enabled: !!propertyId,
  });

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<UnitFormValues>({ resolver: zodResolver(unitSchema) });

  const createMutation = useMutation({
    mutationFn: (values: UnitFormValues) =>
      unitsApi.create(propertyId!, {
        label: values.label,
        sizeSqft: values.sizeSqft ?? null,
        acquisitionValue: values.acquisitionValue ?? null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["propertyasset", "properties", propertyId, "units"],
      });
      reset();
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: unitsApi.archive,
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: ["propertyasset", "properties", propertyId, "units"],
      }),
  });

  if (propertyQuery.isError) {
    return (
      <Stack p="lg">
        <Alert color="red" title="Could not load property" styles={dangerAlertStyles}>
          {propertyQuery.error instanceof TrellisApiError
            ? propertyQuery.error.message
            : "Something went wrong."}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="lg" p="lg">
      <Anchor component={Link} to="/admin/properties" size="sm">
        ← Back to properties
      </Anchor>

      {propertyQuery.data && (
        <Group justify="space-between">
          <div>
            <Title order={2}>{propertyQuery.data.address}</Title>
            <Text c="gray.7" size="sm">
              {propertyQuery.data.propertyType}
              {propertyQuery.data.sizeSqft ? ` · ${propertyQuery.data.sizeSqft} sq ft` : ""}
            </Text>
          </div>
          <Button onClick={openModal}>Add unit / asset</Button>
        </Group>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive unit" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Label</Table.Th>
            <Table.Th>Size (sq ft)</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {unitsQuery.data?.items?.map((unit) => (
            <Table.Tr key={unit.unitAssetId}>
              <Table.Td>{unit.label}</Table.Td>
              <Table.Td>{unit.sizeSqft ?? "—"}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[unit.status ?? ""] ?? "gray.8"}>{unit.status}</Badge>
              </Table.Td>
              <Table.Td>
                {unit.status !== "ARCHIVED" && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      archiveMutation.isPending && archiveMutation.variables === unit.unitAssetId
                    }
                    onClick={() => {
                      if (unit.unitAssetId && confirm(`Archive ${unit.label}?`)) {
                        archiveMutation.mutate(unit.unitAssetId);
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

      {unitsQuery.isSuccess && unitsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No units or assets yet for this property.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Add a unit or asset">
        {createMutation.isError && (
          <Alert color="red" title="Could not add unit" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput
              label="Label"
              placeholder="Unit 4B, Rooftop HVAC"
              error={errors.label?.message}
              {...register("label")}
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
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Add unit / asset
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
