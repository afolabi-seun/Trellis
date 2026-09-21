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
import { useNavigate } from "react-router-dom";
import { tenantsApi } from "../../api/tenants";
import { authApi } from "../../api/auth";
import { TrellisApiError } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";

const provisionSchema = z.object({
  tenantName: z.string().min(1, "Tenant name is required."),
  countryCode: z.string().length(2, "Use a 2-letter ISO 3166-1 country code.").toUpperCase(),
  adminEmail: z.string().email("Enter a valid email address."),
});

type ProvisionFormValues = z.infer<typeof provisionSchema>;

// BDR-NFR-007 (WCAG 2.1 AA), both found via tests/e2e's axe-core scan:
// - Badge's filled variant DOES respect an explicit "color.shade" override — default shade 6
//   fails 4.5:1 white-text contrast for several of these; shade 8 clears it for all five.
// - Alert/Button's "subtle"/"light" variants do NOT respect the same override (confirmed
//   empirically, stayed ~3.1-3.9:1 regardless) — Styles API override on `label` bypasses
//   Mantine's variant color resolver entirely instead. See ResetPasswordPage.tsx's identical
//   constant.
const dangerAlertStyles = { label: { color: "#a61e1e" } };
const dangerButtonStyles = { label: { color: "#a61e1e" } };

const statusColor: Record<string, string> = {
  ACTIVE: "green.8",
  ONBOARDING: "blue.8",
  SUSPENDED: "yellow.9",
  OFFBOARDING: "orange.8",
  OFFBOARDED: "gray.8",
};

/** api_standards.md's `/platform/tenants` — SUPER_ADMIN's tenant list/provisioning screen (roadmap.md's Phase 1 frontend row). */
export function TenantsListPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { claims, logout } = useAuth();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const handleLogout = async () => {
    try {
      await authApi.logout();
    } finally {
      logout();
      navigate("/login", { replace: true });
    }
  };

  const tenantsQuery = useQuery({
    queryKey: ["platform", "tenants"],
    queryFn: () => tenantsApi.list(),
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ProvisionFormValues>({ resolver: zodResolver(provisionSchema) });

  const provisionMutation = useMutation({
    mutationFn: tenantsApi.provision,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["platform", "tenants"] });
      reset();
      closeModal();
    },
  });

  const offboardMutation = useMutation({
    mutationFn: tenantsApi.offboard,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["platform", "tenants"] }),
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Tenants</Title>
        <Group>
          <Text c="gray.7" size="sm">
            {claims?.email}
          </Text>
          <Button onClick={openModal}>Provision tenant</Button>
          <Button variant="subtle" onClick={handleLogout}>
            Sign out
          </Button>
        </Group>
      </Group>

      {tenantsQuery.isError && (
        <Alert color="red" title="Could not load tenants" styles={dangerAlertStyles}>
          {tenantsQuery.error instanceof TrellisApiError
            ? tenantsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Country</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Subscription tier</Table.Th>
            <Table.Th>Created</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {tenantsQuery.data?.items?.map((tenant) => (
            <Table.Tr key={tenant.tenantId}>
              <Table.Td>{tenant.tenantName}</Table.Td>
              <Table.Td>{tenant.countryCode}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[tenant.status ?? ""] ?? "gray.8"}>{tenant.status}</Badge>
              </Table.Td>
              <Table.Td>{tenant.subscriptionTier}</Table.Td>
              <Table.Td>
                {tenant.createdAt ? new Date(tenant.createdAt).toLocaleDateString() : "—"}
              </Table.Td>
              <Table.Td>
                {tenant.status !== "OFFBOARDING" && tenant.status !== "OFFBOARDED" && (
                  <Button
                    size="xs"
                    color="red"
                    styles={dangerButtonStyles}
                    variant="subtle"
                    loading={
                      offboardMutation.isPending && offboardMutation.variables === tenant.tenantId
                    }
                    onClick={() => {
                      if (
                        tenant.tenantId &&
                        confirm(`Begin offboarding ${tenant.tenantName}? This cannot be undone.`)
                      ) {
                        offboardMutation.mutate(tenant.tenantId);
                      }
                    }}
                  >
                    Offboard
                  </Button>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {tenantsQuery.isSuccess && tenantsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No tenants yet. Provision the first one to get started.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="Provision a new tenant">
        {provisionMutation.isError && (
          <Alert color="red" title="Could not provision tenant" mb="md" styles={dangerAlertStyles}>
            {provisionMutation.error instanceof TrellisApiError
              ? provisionMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => provisionMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput
              label="Tenant name"
              error={errors.tenantName?.message}
              {...register("tenantName")}
            />
            <TextInput
              label="Country code"
              placeholder="US"
              maxLength={2}
              error={errors.countryCode?.message}
              {...register("countryCode")}
            />
            <TextInput
              label="Admin email"
              placeholder="admin@tenant.example"
              error={errors.adminEmail?.message}
              {...register("adminEmail")}
            />
            <Button type="submit" loading={provisionMutation.isPending} fullWidth>
              Provision
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
