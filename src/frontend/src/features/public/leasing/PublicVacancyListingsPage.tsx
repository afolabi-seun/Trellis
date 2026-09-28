import { Alert, Anchor, Card, Container, Group, Stack, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { publicLeasingApi } from "../../../api/leasing";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";
import { useTenantSlug } from "../useTenantSlug";

/** BDR-LSE-002 — the anonymous browse page. No login, no account. */
export function PublicVacancyListingsPage() {
  const tenant = useTenantSlug();
  const listingsQuery = useQuery({
    queryKey: ["public-leasing", "listings", tenant.slug],
    queryFn: () => publicLeasingApi.browse(tenant.slug!),
    enabled: Boolean(tenant.slug),
  });

  return (
    <Container size="md" py="xl">
      <Stack gap="lg" component="main">
        <Title order={1}>Available units</Title>
        {tenant.tenantName && <Text c="gray.8">{tenant.tenantName}</Text>}

        {tenant.isNotFound && (
          <Alert color="red" title="Listings unavailable" styles={dangerAlertStyles}>
            These listings are unavailable right now.
          </Alert>
        )}

        {listingsQuery.isError && !tenant.isNotFound && (
          <Alert color="red" title="Could not load listings" styles={dangerAlertStyles}>
            These listings are unavailable right now.
          </Alert>
        )}

        {listingsQuery.data?.items?.map((listing) => (
          <Card key={listing.vacancyListingId} withBorder padding="lg">
            <Group justify="space-between">
              <Stack gap={4}>
                <Anchor
                  component={Link}
                  to={tenant.listingPath(listing.vacancyListingId!)}
                  fw={600}
                >
                  {listing.unitLabel}
                </Anchor>
                <Text size="sm">{listing.description}</Text>
                <Text size="sm" c="gray.7">
                  Available from {listing.availableFrom}
                </Text>
              </Stack>
              <Text fw={700}>
                {formatCurrency(listing.rentAmount ?? 0, listing.currency ?? "USD")} / month
              </Text>
            </Group>
          </Card>
        ))}

        {listingsQuery.isSuccess && listingsQuery.data.items?.length === 0 && (
          <Text c="gray.7">There are no units available right now. Please check back soon.</Text>
        )}
      </Stack>
    </Container>
  );
}
