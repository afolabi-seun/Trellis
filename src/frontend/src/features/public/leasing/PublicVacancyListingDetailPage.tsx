import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Anchor,
  Button,
  Container,
  Group,
  Image,
  Stack,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router-dom";
import { z } from "zod";
import { publicLeasingApi } from "../../../api/leasing";
import { dangerAlertStyles, infoAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";

const emailSchema = z.object({ email: z.string().email("Enter a valid email address.") });
type EmailForm = z.infer<typeof emailSchema>;

/** BDR-LSE-002/008 — photos plus "Apply". The application link goes only to the applicant's own inbox, so the applicant proves control of the email just by using it. */
export function PublicVacancyListingDetailPage() {
  const { tenantId, listingId } = useParams<{ tenantId: string; listingId: string }>();

  const listingQuery = useQuery({
    queryKey: ["public-leasing", "listing", tenantId, listingId],
    queryFn: () => publicLeasingApi.getListing(tenantId!, listingId!),
    enabled: Boolean(tenantId && listingId),
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EmailForm>({ resolver: zodResolver(emailSchema) });

  const linkMutation = useMutation({
    mutationFn: ({ email }: EmailForm) =>
      publicLeasingApi.requestApplicationLink(tenantId!, listingId!, email),
  });

  const listing = listingQuery.data;

  return (
    <Container size="md" py="xl">
      <Stack gap="lg" component="main">
        <Anchor component={Link} to={`/vacancies/${tenantId}`}>
          Back to all units
        </Anchor>

        {listingQuery.isError && (
          <Alert color="red" title="Listing unavailable" styles={dangerAlertStyles}>
            This listing is no longer available.
          </Alert>
        )}

        {listing && (
          <>
            <Title order={1}>{listing.unitLabel}</Title>
            <Group>
              <Text fw={700} size="xl">
                {formatCurrency(listing.rentAmount ?? 0, listing.currency ?? "USD")} / month
              </Text>
              <Text c="gray.7">Available from {listing.availableFrom}</Text>
            </Group>
            <Text>{listing.description}</Text>

            <Group>
              {listing.photoIds?.map((photoId, index) => (
                <Image
                  key={photoId}
                  src={publicLeasingApi.photoUrl(tenantId!, listingId!, photoId)}
                  alt={`${listing.unitLabel} photo ${index + 1}`}
                  w={200}
                  radius="md"
                />
              ))}
            </Group>

            <Title order={2} size="h3">
              Apply for this unit
            </Title>
            {linkMutation.isSuccess ? (
              <Alert color="blue" title="Check your email" styles={infoAlertStyles}>
                If this unit is still available, we&apos;ve sent an application link to your inbox.
              </Alert>
            ) : (
              <form onSubmit={handleSubmit((values) => linkMutation.mutate(values))}>
                <Stack gap="md" maw={400}>
                  <TextInput
                    label="Your email address"
                    description="We'll email you a private link to complete your application."
                    error={errors.email?.message}
                    {...register("email")}
                  />
                  {linkMutation.isError && (
                    <Alert color="red" title="Could not send link" styles={dangerAlertStyles}>
                      Please try again in a moment.
                    </Alert>
                  )}
                  <Button type="submit" loading={linkMutation.isPending}>
                    Email me the application link
                  </Button>
                </Stack>
              </form>
            )}
          </>
        )}
      </Stack>
    </Container>
  );
}
