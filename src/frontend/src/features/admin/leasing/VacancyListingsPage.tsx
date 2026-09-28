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
import { Controller, useForm, useWatch } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { vacancyListingsApi } from "../../../api/leasing";
import { propertiesApi } from "../../../api/properties";
import { unitsApi } from "../../../api/units";
import { formatCurrency } from "../../../utils/formatCurrency";
import {
  dangerAlertStyles,
  dangerButtonStyles,
  infoAlertStyles,
} from "../../../theme/dangerStyles";

const listingSchema = z.object({
  propertyId: z.string().min(1, "Select a property."),
  unitAssetId: z.string().min(1, "Select a unit."),
  rentAmount: z
    .number({ invalid_type_error: "Rent is required." })
    .positive("Rent must be positive."),
  currency: z.string().length(3, "Use a 3-letter currency code."),
  availableFrom: z.string().min(1, "Availability date is required."),
  description: z.string().optional(),
});

type ListingFormValues = z.infer<typeof listingSchema>;

const statusColor: Record<string, string> = {
  LISTED: "success.8",
  UNDER_APPLICATION: "blue.8",
  LEASED: "gray.8",
  WITHDRAWN: "gray.8",
};

/** BDR-LSE-001 — list a vacant unit for lease. Add-on module: an unentitled tenant gets an upsell state rather than a bare error, exact InspectionsListPage precedent. */
export function VacancyListingsPage() {
  const queryClient = useQueryClient();
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const listingsQuery = useQuery({
    queryKey: ["leasing", "listings"],
    queryFn: () => vacancyListingsApi.list(),
  });
  const propertiesQuery = useQuery({
    queryKey: ["propertyasset", "properties"],
    queryFn: () => propertiesApi.list(),
  });

  const {
    control,
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ListingFormValues>({
    resolver: zodResolver(listingSchema),
    defaultValues: { currency: "USD" },
  });

  const selectedPropertyId = useWatch({ control, name: "propertyId" });
  const unitsQuery = useQuery({
    queryKey: ["propertyasset", "units", selectedPropertyId],
    queryFn: () => unitsApi.listByProperty(selectedPropertyId),
    enabled: Boolean(selectedPropertyId),
  });

  const createMutation = useMutation({
    mutationFn: (values: ListingFormValues) =>
      vacancyListingsApi.create({
        unitAssetId: values.unitAssetId,
        rentAmount: values.rentAmount,
        currency: values.currency,
        availableFrom: values.availableFrom,
        description: values.description || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leasing", "listings"] });
      reset({ currency: "USD" });
      closeModal();
    },
  });

  const withdrawMutation = useMutation({
    mutationFn: vacancyListingsApi.withdraw,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["leasing", "listings"] }),
  });

  const isModuleNotEntitled =
    listingsQuery.error instanceof TrellisApiError &&
    listingsQuery.error.code === "TRL-E-BIL-10000";
  if (isModuleNotEntitled) {
    return (
      <Stack gap="md" p="lg">
        <Title order={2}>Vacancy listings</Title>
        <Alert color="blue" title="Leasing isn't enabled for your plan" styles={infoAlertStyles}>
          Vacancy listings and rental applications are an add-on module. Contact your Trellis
          administrator to enable Leasing for your account.
        </Alert>
      </Stack>
    );
  }

  const propertyOptions =
    propertiesQuery.data?.items
      ?.filter((p) => p.status === "ACTIVE")
      .map((p) => ({ value: p.propertyId!, label: p.address! })) ?? [];
  const unitOptions =
    unitsQuery.data?.items
      ?.filter((u) => u.status === "ACTIVE" && u.occupancyStatus === "VACANT")
      .map((u) => ({ value: u.unitAssetId!, label: u.label! })) ?? [];

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Vacancy listings</Title>
        <Button onClick={openModal}>New listing</Button>
      </Group>

      {listingsQuery.isError && (
        <Alert color="red" title="Could not load listings" styles={dangerAlertStyles}>
          {listingsQuery.error instanceof TrellisApiError
            ? listingsQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {withdrawMutation.isError && (
        <Alert color="red" title="Could not withdraw listing" styles={dangerAlertStyles}>
          {withdrawMutation.error instanceof TrellisApiError
            ? withdrawMutation.error.message
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Listing</Table.Th>
            <Table.Th>Rent</Table.Th>
            <Table.Th>Available from</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Photos</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {listingsQuery.data?.items?.map((listing) => (
            <Table.Tr key={listing.vacancyListingId}>
              <Table.Td>
                <Anchor component={Link} to={`/admin/vacancy-listings/${listing.vacancyListingId}`}>
                  {listing.description || "Untitled listing"}
                </Anchor>
              </Table.Td>
              <Table.Td>
                {formatCurrency(listing.rentAmount ?? 0, listing.currency ?? "USD")}
              </Table.Td>
              <Table.Td>{listing.availableFrom}</Table.Td>
              <Table.Td>
                <Badge color={statusColor[listing.status ?? ""] ?? "gray.8"}>
                  {listing.status}
                </Badge>
              </Table.Td>
              <Table.Td>{listing.photos?.length ?? 0}</Table.Td>
              <Table.Td>
                {(listing.status === "LISTED" || listing.status === "UNDER_APPLICATION") && (
                  <Button
                    size="xs"
                    variant="subtle"
                    styles={dangerButtonStyles}
                    loading={
                      withdrawMutation.isPending &&
                      withdrawMutation.variables === listing.vacancyListingId
                    }
                    onClick={() => {
                      if (listing.vacancyListingId && confirm("Withdraw this listing?")) {
                        withdrawMutation.mutate(listing.vacancyListingId);
                      }
                    }}
                  >
                    Withdraw
                  </Button>
                )}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>

      {listingsQuery.isSuccess && listingsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No listings yet. Create the first one to start taking applications.</Text>
      )}

      <Modal opened={modalOpened} onClose={closeModal} title="New vacancy listing">
        {createMutation.isError && (
          <Alert color="red" title="Could not create listing" styles={dangerAlertStyles} mb="md">
            {createMutation.error instanceof TrellisApiError &&
            createMutation.error.code === "TRL-E-LSE-11001"
              ? "That unit already has an active listing."
              : createMutation.error instanceof TrellisApiError
                ? createMutation.error.message
                : "Something went wrong."}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
          <Stack gap="md">
            <Controller
              control={control}
              name="propertyId"
              render={({ field }) => (
                <Select
                  label="Property"
                  data={propertyOptions}
                  value={field.value ?? null}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.propertyId?.message}
                />
              )}
            />
            <Controller
              control={control}
              name="unitAssetId"
              render={({ field }) => (
                <Select
                  label="Unit"
                  description="Only vacant, active units can be listed."
                  data={unitOptions}
                  value={field.value ?? null}
                  onChange={(value) => field.onChange(value ?? "")}
                  error={errors.unitAssetId?.message}
                  disabled={!selectedPropertyId}
                />
              )}
            />
            <Controller
              control={control}
              name="rentAmount"
              render={({ field }) => (
                <NumberInput
                  label="Monthly rent"
                  min={0}
                  value={field.value}
                  onChange={(value) =>
                    field.onChange(typeof value === "number" ? value : undefined)
                  }
                  error={errors.rentAmount?.message}
                />
              )}
            />
            <TextInput
              label="Currency"
              error={errors.currency?.message}
              {...register("currency")}
            />
            <TextInput
              label="Available from"
              type="date"
              error={errors.availableFrom?.message}
              {...register("availableFrom")}
            />
            <TextInput
              label="Description"
              error={errors.description?.message}
              {...register("description")}
            />
            <Button type="submit" loading={createMutation.isPending} fullWidth>
              Create listing
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
