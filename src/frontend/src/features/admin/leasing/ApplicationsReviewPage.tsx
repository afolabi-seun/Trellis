import {
  Alert,
  Badge,
  Button,
  Code,
  Group,
  Modal,
  PasswordInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Timeline,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../../auth/useAuth";
import { TrellisApiError } from "../../../api/client";
import { documentsApi } from "../../../api/documents";
import { applicationsApi, vacancyListingsApi } from "../../../api/leasing";
import { EndLeaseModal, type EndLeaseValues } from "../leases/EndLeaseModal";
import { leaseErrorMessage } from "../leases/leaseLabels";
import {
  dangerAlertStyles,
  dangerButtonStyles,
  infoAlertStyles,
} from "../../../theme/dangerStyles";

const statusColor: Record<string, string> = {
  SUBMITTED: "blue.8",
  SCREENING: "blue.8",
  APPROVED: "success.8",
  DECLINED: "gray.8",
  LEASED: "success.8",
  LEASE_ENDED: "gray.8",
};

const outcomeColor: Record<string, string> = {
  PASS: "success.8",
  FLAGGED: "orange.9",
  FAIL: "red.8",
};

function errorMessage(error: unknown): string {
  if (error instanceof TrellisApiError) {
    if (error.code === "TRL-E-LSE-11003") {
      return "Approve the application first, and only send it for signature once.";
    }
    if (error.code === "TRL-E-LSE-11005") {
      return "Choose a LEASE document filed against this listing's unit — the signed lease is added to it as a new version.";
    }
    if (error.code === "TRL-E-AUT-1004") {
      return "That password isn't correct.";
    }
    return leaseErrorMessage(error);
  }
  return "Something went wrong.";
}

/** BDR-LSE-004 — staff review. Only the coarse screening outcome ever appears here; no endpoint returns the full report. */
export function ApplicationsReviewPage() {
  const { claims } = useAuth();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const listingId = searchParams.get("listing");
  const [timelineFor, setTimelineFor] = useState<string | null>(null);
  const [signFor, setSignFor] = useState<string | null>(null);
  const [draftDocumentId, setDraftDocumentId] = useState<string | null>(null);
  const [signModalOpened, { open: openSignModal, close: closeSignModal }] = useDisclosure(false);
  const [leaseStartDate, setLeaseStartDate] = useState("");
  const [leaseTermType, setLeaseTermType] = useState<string | null>("FIXED");
  const [leaseEndDate, setLeaseEndDate] = useState("");
  const [endLeaseFor, setEndLeaseFor] = useState<string | null>(null);
  const [reportFor, setReportFor] = useState<string | null>(null);
  const [reportPassword, setReportPassword] = useState("");
  const [reportModalOpened, { open: openReportModal, close: closeReportModal }] =
    useDisclosure(false);

  const listingsQuery = useQuery({
    queryKey: ["leasing", "listings"],
    queryFn: () => vacancyListingsApi.list(),
  });
  const listing = listingsQuery.data?.items?.find((l) => l.vacancyListingId === listingId);

  const applicationsQuery = useQuery({
    queryKey: ["leasing", "applications", listingId],
    queryFn: () => applicationsApi.listByListing(listingId!),
    enabled: Boolean(listingId),
  });

  const eventsQuery = useQuery({
    queryKey: ["leasing", "application-events", timelineFor],
    queryFn: () => applicationsApi.getEvents(timelineFor!),
    enabled: Boolean(timelineFor),
  });

  const draftsQuery = useQuery({
    queryKey: ["leasing", "lease-drafts", listing?.unitAssetId],
    queryFn: () => documentsApi.listByUnit(listing!.unitAssetId!),
    enabled: Boolean(listing?.unitAssetId) && signModalOpened,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["leasing", "applications", listingId] });
    queryClient.invalidateQueries({ queryKey: ["leasing", "application-events"] });
  };

  const approveMutation = useMutation({ mutationFn: applicationsApi.approve, onSuccess: refresh });
  const declineMutation = useMutation({ mutationFn: applicationsApi.decline, onSuccess: refresh });
  const monthToMonth = leaseTermType === "MONTH_TO_MONTH";
  const signMutation = useMutation({
    mutationFn: ({ id, documentId }: { id: string; documentId: string }) =>
      applicationsApi.sendForSignature(id, {
        draftDocumentId: documentId,
        leaseStartDate,
        leaseEndDate: monthToMonth ? null : leaseEndDate,
        monthToMonth,
      }),
    onSuccess: () => {
      refresh();
      closeSignModal();
      setDraftDocumentId(null);
      setLeaseStartDate("");
      setLeaseTermType("FIXED");
      setLeaseEndDate("");
    },
  });
  // BDR-LSE-009: a send needs the term — a fixed end date after the start, or month-to-month.
  const termEndBeforeStart =
    !monthToMonth && leaseStartDate !== "" && leaseEndDate !== "" && leaseEndDate <= leaseStartDate;
  const termReady =
    leaseStartDate !== "" && (monthToMonth || (leaseEndDate !== "" && !termEndBeforeStart));
  const endLeaseMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: EndLeaseValues }) =>
      applicationsApi.endLease(id, values),
    onSuccess: () => {
      refresh();
      queryClient.invalidateQueries({ queryKey: ["propertyasset", "units"] });
      queryClient.invalidateQueries({ queryKey: ["leases"] });
      setEndLeaseFor(null);
    },
  });
  const viewReportMutation = useMutation({
    mutationFn: ({ id, currentPassword }: { id: string; currentPassword: string }) =>
      applicationsApi.viewScreeningReport(id, { currentPassword }),
  });

  const isModuleNotEntitled =
    listingsQuery.error instanceof TrellisApiError &&
    listingsQuery.error.code === "TRL-E-BIL-10000";
  if (isModuleNotEntitled) {
    return (
      <Stack gap="md" p="lg">
        <Title order={2}>Applications</Title>
        <Alert color="blue" title="Leasing isn't enabled for your plan" styles={infoAlertStyles}>
          Rental applications are part of the Leasing add-on. Contact your Trellis administrator to
          enable it.
        </Alert>
      </Stack>
    );
  }

  const listingOptions =
    listingsQuery.data?.items?.map((l) => ({
      value: l.vacancyListingId!,
      label: `${l.description || "Untitled listing"} — ${l.status}`,
    })) ?? [];
  const draftOptions =
    draftsQuery.data?.items
      ?.filter((d) => d.documentType === "LEASE")
      .map((d) => ({ value: d.documentId!, label: `Lease draft ${d.documentId!.slice(0, 8)}` })) ??
    [];

  const actionError = approveMutation.error ?? declineMutation.error;

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Applications</Title>

      <Select
        label="Listing"
        placeholder="Choose a listing"
        data={listingOptions}
        value={listingId}
        onChange={(value) => setSearchParams(value ? { listing: value } : {})}
      />

      {(applicationsQuery.isError || actionError) && (
        <Alert color="red" title="Something went wrong" styles={dangerAlertStyles}>
          {errorMessage(applicationsQuery.error ?? actionError)}
        </Alert>
      )}

      {listingId && (
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Applicant</Table.Th>
              <Table.Th>Email</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Screening</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {applicationsQuery.data?.items?.map((application) => {
              const id = application.applicationId!;
              const open = application.status === "SUBMITTED" || application.status === "SCREENING";
              return (
                <Table.Tr key={id}>
                  <Table.Td>{application.applicantFullName}</Table.Td>
                  <Table.Td>{application.applicantEmail}</Table.Td>
                  <Table.Td>
                    <Badge color={statusColor[application.status ?? ""] ?? "gray.8"}>
                      {application.status}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    {application.screeningOutcomeSummary ? (
                      <Badge color={outcomeColor[application.screeningOutcomeSummary] ?? "gray.8"}>
                        {application.screeningOutcomeSummary}
                      </Badge>
                    ) : (
                      <Text size="sm" c="gray.7">
                        Not screened
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <Group gap="xs" justify="flex-end">
                      <Button size="xs" variant="subtle" onClick={() => setTimelineFor(id)}>
                        History
                      </Button>
                      {open && (
                        <Button
                          size="xs"
                          loading={approveMutation.isPending && approveMutation.variables === id}
                          onClick={() => approveMutation.mutate(id)}
                        >
                          Approve
                        </Button>
                      )}
                      {(open || application.status === "APPROVED") && (
                        <Button
                          size="xs"
                          variant="subtle"
                          styles={dangerButtonStyles}
                          loading={declineMutation.isPending && declineMutation.variables === id}
                          onClick={() => {
                            if (confirm("Decline this application?")) declineMutation.mutate(id);
                          }}
                        >
                          Decline
                        </Button>
                      )}
                      {application.status === "APPROVED" && (
                        <Button
                          size="xs"
                          variant="outline"
                          onClick={() => {
                            setSignFor(id);
                            openSignModal();
                          }}
                        >
                          Send for signature
                        </Button>
                      )}
                      {application.status === "LEASED" && (
                        <Button
                          size="xs"
                          variant="subtle"
                          styles={dangerButtonStyles}
                          onClick={() => setEndLeaseFor(id)}
                        >
                          End lease
                        </Button>
                      )}
                      {claims?.role === "TENANT_ADMIN" && application.screeningOutcomeSummary && (
                        <Button
                          size="xs"
                          variant="subtle"
                          onClick={() => {
                            setReportFor(id);
                            openReportModal();
                          }}
                        >
                          View full report
                        </Button>
                      )}
                    </Group>
                  </Table.Td>
                </Table.Tr>
              );
            })}
          </Table.Tbody>
        </Table>
      )}

      {listingId && applicationsQuery.isSuccess && applicationsQuery.data.items?.length === 0 && (
        <Text c="gray.7">No applications for this listing yet.</Text>
      )}
      {!listingId && <Text c="gray.7">Choose a listing to review its applications.</Text>}

      <Modal
        opened={Boolean(timelineFor)}
        onClose={() => setTimelineFor(null)}
        title="Application history"
      >
        <Timeline bulletSize={16}>
          {eventsQuery.data?.map((event) => (
            <Timeline.Item key={event.applicationEventId} title={event.eventType}>
              <Text size="xs" c="gray.7">
                {event.actorType} ·{" "}
                {event.createdAt ? new Date(event.createdAt).toLocaleString() : ""}
              </Text>
            </Timeline.Item>
          ))}
        </Timeline>
      </Modal>

      <Modal opened={signModalOpened} onClose={closeSignModal} title="Send lease for signature">
        <Stack gap="md">
          <Text size="sm">
            Upload the unsigned lease as a LEASE document on the unit first, then choose it here.
          </Text>
          {signMutation.isError && (
            <Alert color="red" title="Could not send for signature" styles={dangerAlertStyles}>
              {errorMessage(signMutation.error)}
            </Alert>
          )}
          <Select
            label="Lease draft"
            data={draftOptions}
            value={draftDocumentId}
            onChange={setDraftDocumentId}
            nothingFoundMessage="No LEASE documents on this unit."
          />
          <TextInput
            label="Lease starts on"
            type="date"
            value={leaseStartDate}
            onChange={(event) => setLeaseStartDate(event.currentTarget.value)}
          />
          <Select
            label="Lease term"
            data={[
              { value: "FIXED", label: "Fixed term" },
              { value: "MONTH_TO_MONTH", label: "Month-to-month" },
            ]}
            value={leaseTermType}
            onChange={setLeaseTermType}
            allowDeselect={false}
          />
          {!monthToMonth && (
            <TextInput
              label="Lease ends on"
              type="date"
              min={leaseStartDate || undefined}
              value={leaseEndDate}
              onChange={(event) => setLeaseEndDate(event.currentTarget.value)}
              error={termEndBeforeStart ? "The end date must be after the start date." : undefined}
            />
          )}
          <Button
            disabled={!draftDocumentId || !signFor || !termReady}
            loading={signMutation.isPending}
            onClick={() => signMutation.mutate({ id: signFor!, documentId: draftDocumentId! })}
          >
            Send for signature
          </Button>
        </Stack>
      </Modal>

      <EndLeaseModal
        opened={endLeaseFor !== null}
        onClose={() => {
          setEndLeaseFor(null);
          endLeaseMutation.reset();
        }}
        description="This ends the lease, stops its rent schedule, and frees the unit to be re-listed."
        loading={endLeaseMutation.isPending}
        error={endLeaseMutation.error}
        onConfirm={(values) => endLeaseMutation.mutate({ id: endLeaseFor!, values })}
      />

      <Modal
        opened={reportModalOpened}
        onClose={() => {
          closeReportModal();
          setReportFor(null);
          setReportPassword("");
          // Drops the decrypted report from memory the instant the modal closes — re-opening it
          // requires the password again, not a cached copy of what was already decrypted.
          viewReportMutation.reset();
        }}
        title="Full screening report"
        size="lg"
      >
        <Stack gap="md">
          {!viewReportMutation.data && (
            <>
              <Text size="sm">
                Re-enter your password to view this applicant's full screening report. This action
                is logged.
              </Text>
              {viewReportMutation.isError && (
                <Alert color="red" title="Could not view report" styles={dangerAlertStyles}>
                  {errorMessage(viewReportMutation.error)}
                </Alert>
              )}
              <PasswordInput
                label="Your password"
                value={reportPassword}
                onChange={(event) => setReportPassword(event.currentTarget.value)}
              />
              <Button
                disabled={!reportPassword || !reportFor}
                loading={viewReportMutation.isPending}
                onClick={() =>
                  viewReportMutation.mutate({ id: reportFor!, currentPassword: reportPassword })
                }
              >
                View report
              </Button>
            </>
          )}
          {viewReportMutation.data && (
            <>
              <Alert color="orange" title="Sensitive information" styles={dangerAlertStyles}>
                This report is not stored in your browser and disappears when you close this window.
              </Alert>
              <Text size="sm" c="gray.7">
                Provider: {viewReportMutation.data.provider} · Outcome:{" "}
                {viewReportMutation.data.outcomeSummary}
              </Text>
              <Code block tabIndex={0}>
                {viewReportMutation.data.reportJson}
              </Code>
            </>
          )}
        </Stack>
      </Modal>
    </Stack>
  );
}
