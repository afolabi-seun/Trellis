import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Box,
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
import { Controller, type Control, useFieldArray, useForm } from "react-hook-form";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { departmentsApi } from "../../../api/departments";
import { workflowTemplatesApi } from "../../../api/workflowTemplates";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const routingRuleSchema = z.object({
  criterion: z.enum(["PROPERTY", "COST_THRESHOLD", "REQUESTING_DEPARTMENT"]),
  operator: z.enum(["EQUALS", "GREATER_THAN_OR_EQUAL"]),
  comparisonValue: z.string().min(1, "Required"),
  targetDepartmentId: z.string().min(1, "Required"),
});

const stepSchema = z.object({
  name: z.string().min(1, "Step name is required."),
  defaultDepartmentId: z.string().min(1, "Required"),
  rules: z.array(routingRuleSchema),
});

const templateSchema = z.object({
  name: z.string().min(1, "Template name is required."),
  description: z.string().optional(),
  requestType: z.string().min(1, "Request type is required."),
  steps: z.array(stepSchema).min(1, "At least one step is required."),
});

type TemplateFormValues = z.infer<typeof templateSchema>;

const statusColor: Record<string, string> = { ACTIVE: "success.8", ARCHIVED: "gray.8" };

interface DepartmentOption {
  value: string;
  label: string;
}

/** BDR-WF-002's conditional routing, made editable: each step has an ordered list of rules, evaluated first-match-wins, with a default department as the always-resolves-to-something fallback. */
function StepRulesEditor({
  control,
  stepIndex,
  departmentOptions,
}: {
  control: Control<TemplateFormValues>;
  stepIndex: number;
  departmentOptions: DepartmentOption[];
}) {
  const { fields, append, remove } = useFieldArray({ control, name: `steps.${stepIndex}.rules` });

  return (
    <Stack gap="xs" pl="md">
      {fields.map((field, ruleIndex) => (
        <Group key={field.id} gap="xs" wrap="wrap">
          <Controller
            control={control}
            name={`steps.${stepIndex}.rules.${ruleIndex}.criterion`}
            render={({ field: controllerField }) => (
              <Select
                data={[
                  { value: "PROPERTY", label: "Property" },
                  { value: "COST_THRESHOLD", label: "Cost threshold" },
                  { value: "REQUESTING_DEPARTMENT", label: "Requesting department" },
                ]}
                value={controllerField.value}
                onChange={(value) => controllerField.onChange(value ?? "")}
                aria-label={`Rule ${ruleIndex + 1} criterion`}
                w={180}
              />
            )}
          />
          <Controller
            control={control}
            name={`steps.${stepIndex}.rules.${ruleIndex}.operator`}
            render={({ field: controllerField }) => (
              <Select
                data={[
                  { value: "EQUALS", label: "Equals" },
                  { value: "GREATER_THAN_OR_EQUAL", label: "Greater than or equal" },
                ]}
                value={controllerField.value}
                onChange={(value) => controllerField.onChange(value ?? "")}
                aria-label={`Rule ${ruleIndex + 1} operator`}
                w={200}
              />
            )}
          />
          <Controller
            control={control}
            name={`steps.${stepIndex}.rules.${ruleIndex}.comparisonValue`}
            render={({ field: controllerField }) => (
              <TextInput
                placeholder="Comparison value"
                aria-label={`Rule ${ruleIndex + 1} comparison value`}
                w={160}
                {...controllerField}
              />
            )}
          />
          <Controller
            control={control}
            name={`steps.${stepIndex}.rules.${ruleIndex}.targetDepartmentId`}
            render={({ field: controllerField }) => (
              <Select
                data={departmentOptions}
                value={controllerField.value}
                onChange={(value) => controllerField.onChange(value ?? "")}
                aria-label={`Rule ${ruleIndex + 1} target department`}
                placeholder="Target department"
                w={200}
              />
            )}
          />
          <Button size="xs" variant="subtle" color="red" onClick={() => remove(ruleIndex)}>
            Remove rule
          </Button>
        </Group>
      ))}
      <Button
        size="xs"
        variant="light"
        onClick={() =>
          append({
            criterion: "PROPERTY",
            operator: "EQUALS",
            comparisonValue: "",
            targetDepartmentId: "",
          })
        }
      >
        Add routing rule
      </Button>
    </Stack>
  );
}

export function WorkflowTemplatesListPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const templatesQuery = useQuery({
    queryKey: ["workflow", "templates"],
    queryFn: () => workflowTemplatesApi.list(),
  });

  const departmentsQuery = useQuery({
    queryKey: ["workflow", "departments"],
    queryFn: () => departmentsApi.list(),
  });

  const departmentOptions: DepartmentOption[] =
    departmentsQuery.data?.items
      ?.filter((d) => d.status === "ACTIVE")
      .map((d) => ({ value: d.departmentId!, label: d.name! })) ?? [];

  const {
    control,
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TemplateFormValues>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      steps: [{ name: "", defaultDepartmentId: "", rules: [] }],
    },
  });

  const {
    fields: stepFields,
    append: appendStep,
    remove: removeStep,
  } = useFieldArray({ control, name: "steps" });

  const createMutation = useMutation({
    mutationFn: (values: TemplateFormValues) =>
      workflowTemplatesApi.create({
        name: values.name,
        description: values.description || null,
        requestType: values.requestType,
        steps: values.steps,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workflow", "templates"] });
      reset();
      closeModal();
    },
  });

  const archiveMutation = useMutation({
    mutationFn: workflowTemplatesApi.archive,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["workflow", "templates"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Workflow templates</Title>
        <Button onClick={openModal}>New template</Button>
      </Group>

      {templatesQuery.isError && (
        <Alert color="red" title="Could not load workflow templates" styles={dangerAlertStyles}>
          {templatesQuery.error instanceof TrellisApiError
            ? templatesQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {archiveMutation.isError && (
        <Alert color="red" title="Could not archive template" styles={dangerAlertStyles}>
          {archiveMutation.error instanceof TrellisApiError
            ? archiveMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Request type</Table.Th>
            <Table.Th>Steps</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {templatesQuery.data?.items?.map((template) => (
            <Table.Tr key={template.workflowTemplateId}>
              <Table.Td>{template.name}</Table.Td>
              <Table.Td>{template.requestType}</Table.Td>
              <Table.Td>{template.steps?.length ?? 0}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[template.status ?? ""] ?? "gray.8"}>
                  {template.status}
                </Badge>
              </Table.Td>
              <Table.Td>
                {template.status !== "ARCHIVED" && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      archiveMutation.isPending &&
                      archiveMutation.variables === template.workflowTemplateId
                    }
                    onClick={() => {
                      if (template.workflowTemplateId && confirm(`Archive ${template.name}?`)) {
                        archiveMutation.mutate(template.workflowTemplateId);
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

      {templatesQuery.isSuccess && templatesQuery.data.items?.length === 0 && (
        <Text c="gray.7">No workflow templates yet. Create the first one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="New workflow template" size="lg">
        {createMutation.isError && (
          <Alert color="red" title="Could not create template" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput label="Name" error={errors.name?.message} {...register("name")} />
            <TextInput
              label="Request type"
              placeholder="e.g. MAINTENANCE"
              error={errors.requestType?.message}
              {...register("requestType")}
            />
            <TextInput
              label="Description"
              error={errors.description?.message}
              {...register("description")}
            />

            <Text fw={600}>Steps</Text>
            {stepFields.map((step, stepIndex) => (
              <Box
                key={step.id}
                p="sm"
                style={{ border: "1px solid var(--mantine-color-gray-3)", borderRadius: 4 }}
              >
                <Group align="flex-end" gap="xs" wrap="wrap">
                  <TextInput
                    label={`Step ${stepIndex + 1} name`}
                    error={errors.steps?.[stepIndex]?.name?.message}
                    {...register(`steps.${stepIndex}.name` as const)}
                    w={220}
                  />
                  <Controller
                    control={control}
                    name={`steps.${stepIndex}.defaultDepartmentId`}
                    render={({ field: controllerField }) => (
                      <Select
                        label="Default department"
                        data={departmentOptions}
                        error={errors.steps?.[stepIndex]?.defaultDepartmentId?.message}
                        value={controllerField.value}
                        onChange={(value) => controllerField.onChange(value ?? "")}
                        w={220}
                      />
                    )}
                  />
                  {stepFields.length > 1 && (
                    <Button
                      size="xs"
                      variant="subtle"
                      color="red"
                      onClick={() => removeStep(stepIndex)}
                    >
                      Remove step
                    </Button>
                  )}
                </Group>
                <StepRulesEditor
                  control={control}
                  stepIndex={stepIndex}
                  departmentOptions={departmentOptions}
                />
              </Box>
            ))}
            <Button
              size="xs"
              variant="light"
              onClick={() => appendStep({ name: "", defaultDepartmentId: "", rules: [] })}
            >
              Add step
            </Button>

            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Create template
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
