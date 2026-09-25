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
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { tenantVendorRelationshipsApi } from "../../../api/tenantVendorRelationships";
import { vendorsApi } from "../../../api/vendors";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";

const vendorSchema = z.object({
  vendorName: z.string().min(1, "Vendor name is required."),
  category: z.enum(["MAINTENANCE", "LEGAL", "INSPECTION", "OTHER"]),
});

type VendorFormValues = z.infer<typeof vendorSchema>;

const inviteSchema = z.object({
  email: z.string().email("Enter a valid email address."),
});

type InviteFormValues = z.infer<typeof inviteSchema>;

const relationshipStatusColor: Record<string, string> = {
  ACTIVE: "success.8",
  TERMINATED: "gray.8",
};

/** BDR-VEN-001/004/006 — onboard a vendor (or find one already onboarded by another tenant), invite its login, manage the engagement lifecycle. */
export function VendorsListPage() {
  const queryClient = useQueryClient();
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] =
    useDisclosure(false);
  const [inviteVendorId, setInviteVendorId] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const searchQuery = useQuery({
    queryKey: ["vendor", "vendors", search],
    queryFn: () => vendorsApi.search(search || undefined),
  });

  const relationshipsQuery = useQuery({
    queryKey: ["vendor", "relationships"],
    queryFn: () => tenantVendorRelationshipsApi.list(),
  });

  const relationshipByVendorId = new Map(
    (relationshipsQuery.data?.items ?? []).map((r) => [r.vendorId, r]),
  );

  const {
    register: registerVendor,
    handleSubmit: handleSubmitVendor,
    reset: resetVendor,
    control: vendorControl,
    formState: { errors: vendorErrors },
  } = useForm<VendorFormValues>({
    resolver: zodResolver(vendorSchema),
    defaultValues: { category: "MAINTENANCE" },
  });

  const {
    register: registerInvite,
    handleSubmit: handleSubmitInvite,
    reset: resetInvite,
    formState: { errors: inviteErrors },
  } = useForm<InviteFormValues>({ resolver: zodResolver(inviteSchema) });

  const createMutation = useMutation({
    mutationFn: (values: VendorFormValues) => vendorsApi.create(values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["vendor", "vendors"] });
      queryClient.invalidateQueries({ queryKey: ["vendor", "relationships"] });
      resetVendor();
      closeCreateModal();
    },
  });

  const linkMutation = useMutation({
    mutationFn: (vendorId: string) => tenantVendorRelationshipsApi.create(vendorId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["vendor", "relationships"] }),
  });

  const terminateMutation = useMutation({
    mutationFn: (relationshipId: string) => tenantVendorRelationshipsApi.terminate(relationshipId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["vendor", "relationships"] }),
  });

  const inviteMutation = useMutation({
    mutationFn: ({ vendorId, values }: { vendorId: string; values: InviteFormValues }) =>
      vendorsApi.inviteUser(vendorId, values),
    onSuccess: () => {
      resetInvite();
      setInviteVendorId(null);
    },
  });

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Vendors</Title>
        <Button onClick={openCreateModal}>Onboard vendor</Button>
      </Group>

      <TextInput
        label="Search vendors"
        placeholder="Find a vendor already onboarded by another tenant"
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
      />

      {searchQuery.isError && (
        <Alert color="red" title="Could not load vendors" styles={dangerAlertStyles}>
          {searchQuery.error instanceof TrellisApiError
            ? searchQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Category</Table.Th>
            <Table.Th>Relationship</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {searchQuery.data?.items?.map((vendor) => {
            const relationship = vendor.vendorId
              ? relationshipByVendorId.get(vendor.vendorId)
              : undefined;
            return (
              <Table.Tr key={vendor.vendorId}>
                <Table.Td>{vendor.vendorName}</Table.Td>
                <Table.Td>{vendor.category}</Table.Td>
                <Table.Td>
                  {relationship ? (
                    <Badge color={relationshipStatusColor[relationship.status ?? ""] ?? "gray.8"}>
                      {relationship.status}
                    </Badge>
                  ) : (
                    <Button
                      size="xs"
                      variant="light"
                      loading={linkMutation.isPending && linkMutation.variables === vendor.vendorId}
                      onClick={() => vendor.vendorId && linkMutation.mutate(vendor.vendorId)}
                    >
                      Engage this vendor
                    </Button>
                  )}
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <Button
                      size="xs"
                      variant="subtle"
                      onClick={() => setInviteVendorId(vendor.vendorId ?? null)}
                    >
                      Invite login
                    </Button>
                    {relationship?.status === "ACTIVE" && (
                      <Button
                        size="xs"
                        variant="subtle"
                        styles={dangerButtonStyles}
                        loading={
                          terminateMutation.isPending &&
                          terminateMutation.variables === relationship.tenantVendorRelationshipId
                        }
                        onClick={() =>
                          relationship.tenantVendorRelationshipId &&
                          confirm(
                            `Terminate the relationship with ${vendor.vendorName}? Existing assignments stay visible to them.`,
                          ) &&
                          terminateMutation.mutate(relationship.tenantVendorRelationshipId)
                        }
                      >
                        Terminate
                      </Button>
                    )}
                  </Group>
                </Table.Td>
              </Table.Tr>
            );
          })}
        </Table.Tbody>
      </Table>

      {searchQuery.isSuccess && searchQuery.data.items?.length === 0 && (
        <Text c="gray.7">No vendors found. Onboard one to get started.</Text>
      )}

      <Modal opened={createModalOpened} onClose={closeCreateModal} title="Onboard a vendor">
        {createMutation.isError && (
          <Alert color="red" title="Could not onboard vendor" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError
              ? createMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmitVendor((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <TextInput
              label="Vendor name"
              error={vendorErrors.vendorName?.message}
              {...registerVendor("vendorName")}
            />
            <Controller
              control={vendorControl}
              name="category"
              render={({ field }) => (
                <Select
                  label="Category"
                  data={[
                    { value: "MAINTENANCE", label: "Maintenance" },
                    { value: "LEGAL", label: "Legal" },
                    { value: "INSPECTION", label: "Inspection" },
                    { value: "OTHER", label: "Other" },
                  ]}
                  value={field.value}
                  onChange={(value) => field.onChange(value ?? "OTHER")}
                />
              )}
            />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Onboard vendor
            </Button>
          </Stack>
        </form>
      </Modal>

      <Modal
        opened={inviteVendorId !== null}
        onClose={() => setInviteVendorId(null)}
        title="Invite the vendor's login"
      >
        {inviteMutation.isError && (
          <Alert color="red" title="Could not invite user" styles={dangerAlertStyles} mb="md">
            {inviteMutation.error instanceof TrellisApiError
              ? inviteMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}
        {inviteMutation.isSuccess && (
          <Alert color="green" title="Invitation sent" mb="md">
            A password-set link was sent to this address.
          </Alert>
        )}

        <form
          onSubmit={handleSubmitInvite((values) => {
            if (inviteVendorId) inviteMutation.mutate({ vendorId: inviteVendorId, values });
          })}
        >
          <Stack gap="md">
            <TextInput
              label="Email"
              error={inviteErrors.email?.message}
              {...registerInvite("email")}
            />
            <Button type="submit" loading={inviteMutation.isPending} fullWidth>
              Send invitation
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
