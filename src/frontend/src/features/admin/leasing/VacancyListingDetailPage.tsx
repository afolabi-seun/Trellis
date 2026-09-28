import {
  Alert,
  Anchor,
  Badge,
  Button,
  FileButton,
  Group,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { TrellisApiError } from "../../../api/client";
import { vacancyListingsApi } from "../../../api/leasing";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";

const scanColor: Record<string, string> = {
  CLEAN: "success.8",
  PENDING: "blue.8",
  INFECTED: "red.8",
  SCAN_FAILED: "red.8",
};

/** BDR-LSE-001 — one listing's terms and photo gallery. A photo only ever appears on the public listing once its malware scan is CLEAN. */
export function VacancyListingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const listingQuery = useQuery({
    queryKey: ["leasing", "listing", id],
    queryFn: () => vacancyListingsApi.getById(id!),
    enabled: Boolean(id),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => vacancyListingsApi.uploadPhoto(id!, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leasing", "listing", id] });
      queryClient.invalidateQueries({ queryKey: ["leasing", "listings"] });
    },
  });

  const downloadMutation = useMutation({
    mutationFn: async ({ photoId, fileName }: { photoId: string; fileName: string }) => {
      const blob = await vacancyListingsApi.downloadPhoto(id!, photoId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    },
  });

  const listing = listingQuery.data;

  return (
    <Stack gap="lg" p="lg">
      <Group justify="space-between">
        <Title order={2}>Listing</Title>
        <Anchor component={Link} to={`/admin/applications?listing=${id}`}>
          Review applications
        </Anchor>
      </Group>

      {listingQuery.isError && (
        <Alert color="red" title="Could not load listing" styles={dangerAlertStyles}>
          {listingQuery.error instanceof TrellisApiError
            ? listingQuery.error.message
            : "Something went wrong."}
        </Alert>
      )}

      {listing && (
        <Stack gap="xs">
          <Group>
            <Text fw={700} size="xl">
              {formatCurrency(listing.rentAmount ?? 0, listing.currency ?? "USD")} / month
            </Text>
            <Badge color="blue.8">{listing.status}</Badge>
          </Group>
          <Text c="gray.7">Available from {listing.availableFrom}</Text>
          <Text>{listing.description}</Text>
        </Stack>
      )}

      <Group justify="space-between">
        <Title order={3}>Photos</Title>
        <FileButton
          accept="image/jpeg,image/png,image/heic"
          onChange={(file) => file && uploadMutation.mutate(file)}
        >
          {(props) => (
            <Button {...props} loading={uploadMutation.isPending}>
              Upload photo
            </Button>
          )}
        </FileButton>
      </Group>

      {uploadMutation.isError && (
        <Alert color="red" title="Could not upload photo" styles={dangerAlertStyles}>
          {uploadMutation.error instanceof TrellisApiError &&
          uploadMutation.error.code === "TRL-E-LSE-11004"
            ? "Photos must be JPG, PNG or HEIC, and a listing holds a limited number."
            : "Something went wrong."}
        </Alert>
      )}

      <Table striped>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>File</Table.Th>
            <Table.Th>Scan</Table.Th>
            <Table.Th />
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {listing?.photos?.map((photo) => (
            <Table.Tr key={photo.vacancyListingPhotoId}>
              <Table.Td>{photo.fileName}</Table.Td>
              <Table.Td>
                <Badge color={scanColor[photo.scanStatus ?? ""] ?? "gray.8"}>
                  {photo.scanStatus}
                </Badge>
              </Table.Td>
              <Table.Td>
                <Button
                  size="xs"
                  variant="subtle"
                  onClick={() =>
                    downloadMutation.mutate({
                      photoId: photo.vacancyListingPhotoId!,
                      fileName: photo.fileName ?? "photo",
                    })
                  }
                >
                  Download
                </Button>
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
      {listing && (listing.photos?.length ?? 0) === 0 && <Text c="gray.7">No photos yet.</Text>}
    </Stack>
  );
}
