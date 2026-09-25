import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  NumberInput,
  Select,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { tenantVendorRelationshipsApi } from "../../../api/tenantVendorRelationships";
import { vendorAssignmentsApi } from "../../../api/vendorAssignments";
import { vendorsApi } from "../../../api/vendors";
import { workflowInstancesApi } from "../../../api/workflowInstances";
import { workflowTemplatesApi } from "../../../api/workflowTemplates";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const instanceSchema = z.object({
  workflowTemplateId: z.string().min(1, "A template is required."),
  costAmount: z.number().optional(),
});

type InstanceFormValues = z.infer<typeof instanceSchema>;

const instanceStatusColor: Record<string, string> = {
  IN_PROGRESS: "blue.8",
  APPROVED: "success.8",
  REJECTED: "red.8",
  CANCELLED: "gray.8",
};

const stepStatusColor: Record<string, string> = {
  PENDING: "gray.6",
  IN_PROGRESS: "blue.8",
  APPROVED: "success.8",
  REJECTED: "red.8",
  SKIPPED: "gray.6",
};

/** BDR-WF's exit criterion made visible: every instance's full step chain, with approve/reject on whichever step is currently awaiting a decision — the rest of BDR-WF-004's audit trail. */
export function WorkflowInstancesBoardPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [assigningInstanceId, setAssigningInstanceId] = useState<string | null>(null);
  const [selectedVendorId, setSelectedVendorId] = useState<string | null>(null);

  const instancesQuery = useQuery({
    queryKey: ["workflow", "instances"],
    queryFn: () => workflowInstancesApi.list(),
  });

  const templatesQuery = useQuery({
    queryKey: ["workflow", "templates"],
    queryFn: () => workflowTemplatesApi.list(),
  });

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

  const assignVendorMutation = useMutation({
    mutationFn: ({
      workflowInstanceId,
      vendorId,
    }: {
      workflowInstanceId: string;
      vendorId: string;
    }) =>
      vendorAssignmentsApi.create({
        vendorId,
        workItemType: "WORKFLOW_INSTANCE",
        workItemId: workflowInstanceId,
      }),
    onSuccess: () => {
      setAssigningInstanceId(null);
      setSelectedVendorId(null);
    },
  });

  const templateOptions =
    templatesQuery.data?.items
      ?.filter((t) => t.status === "ACTIVE")
      .map((t) => ({ value: t.workflowTemplateId!, label: `${t.name} (${t.requestType})` })) ?? [];

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<InstanceFormValues>({ resolver: zodResolver(instanceSchema) });

  const createMutation = useMutation({
    mutationFn: (values: InstanceFormValues) =>
      workflowInstancesApi.create({
        workflowTemplateId: values.workflowTemplateId,
        propertyId: null,
        requestingDepartmentId: null,
        costAmount: values.costAmount ?? null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workflow", "instances"] });
      reset();
      closeModal();
    },
  });

  const approveMutation = useMutation({
    mutationFn: (workflowInstanceId: string) =>
      workflowInstancesApi.approve(workflowInstanceId, { note: null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow", "instances"] }),
  });

  const rejectMutation = useMutation({
    mutationFn: (workflowInstanceId: string) =>
      workflowInstancesApi.reject(workflowInstanceId, { note: null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow", "instances"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Workflow instances</Title>
        <Button onClick={openModal}>New instance</Button>
      </Group>

      {instancesQuery.isError && (
        <Alert color="red" title="Could not load workflow instances" styles={dangerAlertStyles}>
          {instancesQuery.error instanceof TrellisApiError
            ? instancesQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {(approveMutation.isError || rejectMutation.isError) && (
        <Alert color="red" title="Could not record decision" styles={dangerAlertStyles}>
          {approveMutation.error instanceof TrellisApiError
            ? approveMutation.error.message
            : rejectMutation.error instanceof TrellisApiError
              ? rejectMutation.error.message
              : "You may not be authorized to act on this step."}
        </Alert>
      )}

      {assignVendorMutation.isError && (
        <Alert color="red" title="Could not assign vendor" styles={dangerAlertStyles}>
          {assignVendorMutation.error instanceof TrellisApiError
            ? assignVendorMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Stack gap="md">
        {instancesQuery.data?.items?.map((instance) => {
          const currentStep = instance.steps?.find((s) => s.status === "IN_PROGRESS");
          return (
            <Stack
              key={instance.workflowInstanceId}
              gap="xs"
              p="md"
              style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
            >
              <Group justify="space-between">
                <Group gap="sm">
                  <Text fw={600}>Instance {instance.workflowInstanceId?.slice(0, 8)}</Text>
                  <Badge color={instanceStatusColor[instance.status ?? ""] ?? "gray.8"}>
                    {instance.status}
                  </Badge>
                </Group>
                <Group gap="xs">
                  <Button
                    size="xs"
                    variant="subtle"
                    onClick={() => {
                      setAssigningInstanceId(instance.workflowInstanceId ?? null);
                      setSelectedVendorId(null);
                    }}
                  >
                    Assign vendor
                  </Button>
                  {currentStep && (
                    <>
                      <Button
                        size="xs"
                        loading={
                          approveMutation.isPending &&
                          approveMutation.variables === instance.workflowInstanceId
                        }
                        onClick={() =>
                          instance.workflowInstanceId &&
                          approveMutation.mutate(instance.workflowInstanceId)
                        }
                      >
                        Approve
                      </Button>
                      <Button
                        size="xs"
                        variant="outline"
                        styles={dangerButtonStyles}
                        loading={
                          rejectMutation.isPending &&
                          rejectMutation.variables === instance.workflowInstanceId
                        }
                        onClick={() =>
                          instance.workflowInstanceId &&
                          rejectMutation.mutate(instance.workflowInstanceId)
                        }
                      >
                        Reject
                      </Button>
                    </>
                  )}
                </Group>
              </Group>

              {assigningInstanceId === instance.workflowInstanceId && (
                <Group align="flex-end" gap="xs">
                  <Select
                    label="Vendor"
                    placeholder="Pick an actively engaged vendor"
                    data={activeVendorOptions}
                    value={selectedVendorId}
                    onChange={setSelectedVendorId}
                    w={260}
                  />
                  <Button
                    size="xs"
                    loading={assignVendorMutation.isPending}
                    disabled={!selectedVendorId}
                    onClick={() =>
                      instance.workflowInstanceId &&
                      selectedVendorId &&
                      assignVendorMutation.mutate({
                        workflowInstanceId: instance.workflowInstanceId,
                        vendorId: selectedVendorId,
                      })
                    }
                  >
                    Assign
                  </Button>
                  <Button size="xs" variant="subtle" onClick={() => setAssigningInstanceId(null)}>
                    Cancel
                  </Button>
                </Group>
              )}

              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Step</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Decision note</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {instance.steps?.map((step) => (
                    <Table.Tr key={step.workflowStepInstanceId}>
                      <Table.Td>{step.name}</Table.Td>
                      <Table.Td>
                        <Badge color={stepStatusColor[step.status ?? ""] ?? "gray.6"}>
                          {step.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{step.decisionNote ?? "—"}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Stack>
          );
        })}
      </Stack>

      {instancesQuery.isSuccess && instancesQuery.data.items?.length === 0 && (
        <Text c="gray.7">No workflow instances yet. Create one from an active template.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="New workflow instance">
        {createMutation.isError && (
          <Alert color="red" title="Could not create instance" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={control}
              name="workflowTemplateId"
              render={({ field }) => (
                <Select
                  label="Template"
                  data={templateOptions}
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.workflowTemplateId?.message}
                />
              )}
            />
            <Controller
              control={control}
              name="costAmount"
              render={({ field }) => (
                <NumberInput
                  label="Cost amount"
                  min={0}
                  decimalScale={2}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                />
              )}
            />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Create instance
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
