import { Alert, Anchor, Card, Container, Group, Stack, Text, Title } from "@mantine/core";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { publicLeasingApi } from "../../../api/leasing";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";

/** BDR-LSE-002 — the anonymous browse page. No login, no account. */
export function PublicVacancyListingsPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const listingsQuery = useQuery({
    queryKey: ["public-leasing", "listings", tenantId],
    queryFn: () => publicLeasingApi.browse(tenantId!),
    enabled: Boolean(tenantId),
  });

  return (
    <Container size="md" py="xl">
      <Stack gap="lg" component="main">
        <Title order={1}>Available units</Title>

        {listingsQuery.isError && (
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
                  to={`/vacancies/${tenantId}/${listing.vacancyListingId}`}
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
