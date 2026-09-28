import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  Select,
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
import { Link } from "react-router-dom";
import { z } from "zod";
import { inspectionsApi } from "../../../api/inspections";
import { TrellisApiError } from "../../../api/client";
import { propertiesApi } from "../../../api/properties";
import { tenantVendorRelationshipsApi } from "../../../api/tenantVendorRelationships";
import { vendorAssignmentsApi } from "../../../api/vendorAssignments";
import { vendorsApi } from "../../../api/vendors";
import { dangerAlertStyles, infoAlertStyles } from "../../../theme/dangerStyles";

const scheduleSchema = z.object({
  propertyId: z.string().min(1, "A property is required."),
  scheduledAt: z.string().min(1, "A scheduled date is required."),
});

type ScheduleFormValues = z.infer<typeof scheduleSchema>;

const inspectionStatusColor: Record<string, string> = {
  SCHEDULED: "gray.8",
  IN_PROGRESS: "blue.8",
  CLOSED: "success.8",
};

/**
 * BDR-INSP-001 — schedule an inspection against a Property, then assign it either to internal
 * staff (via the API, not exposed here — no existing "pick a staff user" picker anywhere in this
 * codebase to reuse) or a Vendor (the flagship path this phase's exit criterion actually tests,
 * reusing the exact same vendor-assignment flow WorkflowInstancesBoardPage.tsx already has).
 */
export function InspectionsListPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [assigningInspectionId, setAssigningInspectionId] = useState<string | null>(null);
  const [selectedVendorId, setSelectedVendorId] = useState<string | null>(null);

  const inspectionsQuery = useQuery({
    queryKey: ["inspection", "inspections"],
    queryFn: () => inspectionsApi.list(),
  });

  const propertiesQuery = useQuery({
    queryKey: ["property", "properties"],
    queryFn: () => propertiesApi.list(),
  });

  const propertyOptions =
    propertiesQuery.data?.items?.map((p) => ({
      value: p.propertyId!,
      label: p.address ?? p.propertyId!,
    })) ?? [];
  const propertyAddressById = new Map(
    propertiesQuery.data?.items?.map((p) => [p.propertyId, p.address]) ?? [],
  );

  const relationshipsQuery = useQuery({
    queryKey: ["vendor", "relationships"],
    queryFn: () => tenantVendorRelationshipsApi.list(),
  });

  const vendorsQuery = useQuery({
    queryKey: ["vendor", "vendors"],
    queryFn: () => vendorsApi.search(),
  });

  const vendorNameById = new Map(
    (vendorsQuery.data?.items ?? []).map((v) => [v.vendorId, v.vendorName]),
  );
  const activeVendorOptions =
    relationshipsQuery.data?.items
      ?.filter((r) => r.status === "ACTIVE")
      .map((r) => ({ value: r.vendorId!, label: vendorNameById.get(r.vendorId) ?? r.vendorId! })) ??
    [];

  const {
    control,
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ScheduleFormValues>({ resolver: zodResolver(scheduleSchema) });

  const scheduleMutation = useMutation({
    mutationFn: (values: ScheduleFormValues) =>
      inspectionsApi.create({
        propertyId: values.propertyId,
        unitAssetId: null,
        assignedToUserId: null,
        scheduledAt: new Date(values.scheduledAt).toISOString(),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inspection", "inspections"] });
      reset();
      closeModal();
    },
  });

  const assignVendorMutation = useMutation({
    mutationFn: ({ inspectionId, vendorId }: { inspectionId: string; vendorId: string }) =>
      vendorAssignmentsApi.create({
        vendorId,
        workItemType: "INSPECTION",
        workItemId: inspectionId,
      }),
    onSuccess: () => {
      setAssigningInspectionId(null);
      setSelectedVendorId(null);
      queryClient.invalidateQueries({ queryKey: ["inspection", "inspections"] });
    },
  });

  // billing_design.md's frontend_architecture.md carve-out — the one place a module-entitlement
  // 403 is treated differently from every other error: an upsell state, not a bare error message,
  // since the tenant's own commercial status (not a bug or a permissions mistake) is why nothing
  // loads here.
  const isModuleNotEntitled =
    inspectionsQuery.error instanceof TrellisApiError &&
    inspectionsQuery.error.code === "TRL-E-BIL-10000";

  if (isModuleNotEntitled) {
    return (
      <Stack gap="lg" p="lg">
        <Title order={2}>Inspections</Title>
        <Alert
          color="blue"
          title="Inspections isn't enabled for your plan"
          styles={infoAlertStyles}
        >
          Ask your Trellis account manager to enable the Inspections module to start scheduling and
          tracking property inspections.
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Inspections</Title>
        <Button onClick={openModal}>Schedule inspection</Button>
      </Group>

      {inspectionsQuery.isError && (
        <Alert color="red" title="Could not load inspections" styles={dangerAlertStyles}>
          {inspectionsQuery.error instanceof TrellisApiError
            ? inspectionsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {assignVendorMutation.isError && (
        <Alert color="red" title="Could not assign vendor" styles={dangerAlertStyles}>
          {assignVendorMutation.error instanceof TrellisApiError
            ? assignVendorMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Property</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Scheduled</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {inspectionsQuery.data?.items?.map((inspection) => (
            <Table.Tr key={inspection.inspectionId}>
              <Table.Td>
                <Text
                  component={Link}
                  to={`/admin/inspections/${inspection.inspectionId}`}
                  fw={600}
                >
                  {(inspection.propertyId && propertyAddressById.get(inspection.propertyId)) ??
                    inspection.propertyId ??
                    "—"}
                </Text>
              </Table.Td>
              <Table.Td>
                <Badge color={inspectionStatusColor[inspection.status ?? ""] ?? "gray.6"}>
                  {inspection.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {inspection.scheduledAt ? new Date(inspection.scheduledAt).toLocaleString() : "—"}
              </Table.Td>
              <Table.Td>
                {inspection.status === "SCHEDULED" && (
                  <Group gap="xs" align="flex-end">
                    {assigningInspectionId === inspection.inspectionId ? (
                      <>
                        <Select
                          placeholder="Pick a vendor"
                          data={activeVendorOptions}
                          value={selectedVendorId}
                          onChange={setSelectedVendorId}
                          w={200}
                        />
                        <Button
                          size="xs"
                          loading={assignVendorMutation.isPending}
                          disabled={!selectedVendorId}
                          onClick={() =>
                            inspection.inspectionId &&
                            selectedVendorId &&
                            assignVendorMutation.mutate({
                              inspectionId: inspection.inspectionId,
                              vendorId: selectedVendorId,
                            })
                          }
                        >
                          Assign
                        </Button>
                        <Button
                          size="xs"
                          variant="subtle"
                          onClick={() => setAssigningInspectionId(null)}
                        >
                          Cancel
                        </Button>
                      </>
                    ) : (
                      <Button
                        size="xs"
                        variant="light"
                        onClick={() => setAssigningInspectionId(inspection.inspectionId ?? null)}
                      >
                        Assign vendor
                      </Button>
                    )}
                  </Group>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {inspectionsQuery.isSuccess && inspectionsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No inspections yet. Schedule one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Schedule an inspection">
        {scheduleMutation.isError && (
          <Alert
            color="red"
            title="Could not schedule inspection"
            styles={dangerAlertStyles}
            mb="md"
          >
            {scheduleMutation.error instanceof TrellisApiError
              ? scheduleMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => scheduleMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={control}
              name="propertyId"
              render={({ field }) => (
                <Select
                  label="Property"
                  data={propertyOptions}
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.propertyId?.message}
                />
              )}
            />
            <TextInput
              label="Scheduled for"
              type="datetime-local"
              error={errors.scheduledAt?.message}
              {...register("scheduledAt")}
            />
            <Button type="submit" loading={scheduleMutation.isPending} fullWidth>
              Schedule inspection
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
