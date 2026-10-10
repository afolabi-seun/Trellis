import {
  Alert,
  Badge,
  Button,
  Modal,
  Select,
  Stack,
  Table,
  Tabs,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { TrellisApiError } from "../../../api/client";
import { renewalOffersApi } from "../../../api/leasing";
import type { components } from "../../../api/schema";
import { leasesApi } from "../../../api/leases";
import { formatCurrency } from "../../../utils/formatCurrency";
import { useTenantSettings } from "../settings/useTenantSettings";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { leaseErrorMessage, termLabel } from "./leaseLabels";
import { OfferRenewalForm, RecordRenewalAnswerForm } from "./RenewalForms";

type Lease = components["schemas"]["LeaseResponse"];
type RenewalOffer = components["schemas"]["RenewalOfferResponse"];

const RENEWAL_STATUS: Record<string, { label: string; color: string }> = {
  OFFERED: { label: "Offered", color: "blue.8" },
  ACCEPTED: { label: "Accepted", color: "success.8" },
  DECLINED: { label: "Declined", color: "gray.8" },
  EXPIRED: { label: "No answer", color: "gray.8" },
};

/** Renewal actions for one lease row, when the tenant has Leasing (BDR-LSE-010). */
interface RenewalActions {
  openOfferFor: (leaseId?: string) => RenewalOffer | undefined;
  onOffer: (lease: Lease) => void;
  onAnswer: (offer: RenewalOffer) => void;
}

/** BDR-LSE-009's three horizons, plus leases already past their end date and still active. */
const BUCKETS = [
  { value: "30", label: "Within 30 days", test: (d: number) => d >= 0 && d <= 30 },
  { value: "60", label: "31–60 days", test: (d: number) => d > 30 && d <= 60 },
  { value: "90", label: "61–90 days", test: (d: number) => d > 60 && d <= 90 },
  { value: "past", label: "Past end date", test: (d: number) => d < 0 },
];

const sourceLabel = (source?: string | null) =>
  source === "LEASING" ? "Leasing" : source === "MANUAL" ? "Rent schedule" : "—";

function daysLabel(days?: number | null) {
  if (days == null) return "—";
  if (days < 0) return `${-days} day${days === -1 ? "" : "s"} ago`;
  if (days === 0) return "Today";
  return `${days} day${days === 1 ? "" : "s"}`;
}

/**
 * BDR-LSE-009 — leases ending in the next 90 days, so renewals and re-letting can start in time,
 * and the leases whose term was never recorded, so staff can fill each one in. Leases from
 * Leasing and from manually started rent schedules appear together: a lease is a lease,
 * whichever add-on started it.
 */
export function LeasesPage() {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<string | null>("30");
  const [termFor, setTermFor] = useState<Lease | null>(null);
  const [offerFor, setOfferFor] = useState<Lease | null>(null);
  const [answerFor, setAnswerFor] = useState<RenewalOffer | null>(null);
  const settingsQuery = useTenantSettings();

  const expiringQuery = useQuery({
    queryKey: ["leases", "expiring"],
    queryFn: () => leasesApi.listExpiring(90),
  });
  const notRecordedQuery = useQuery({
    queryKey: ["leases", "term-not-recorded"],
    queryFn: () => leasesApi.listTermNotRecorded(),
  });

  // Renewal offers are part of the Leasing add-on; without it the API answers TRL-E-BIL-10000
  // and the page simply shows no renewal features.
  const renewalsQuery = useQuery({
    queryKey: ["leases", "renewals"],
    queryFn: () => renewalOffersApi.list(),
    retry: false,
  });
  const renewalsEnabled = !(
    renewalsQuery.error instanceof TrellisApiError && renewalsQuery.error.code === "TRL-E-BIL-10000"
  );

  const expiring = expiringQuery.data ?? [];
  const notRecorded = notRecordedQuery.data ?? [];
  const renewals = renewalsQuery.data ?? [];
  const openRenewals = renewals.filter((o) => o.status === "OFFERED");
  const currency = settingsQuery.data?.defaultCurrency ?? "USD";
  const loadError =
    expiringQuery.error ?? notRecordedQuery.error ?? (renewalsEnabled ? renewalsQuery.error : null);
  const renewalActions: RenewalActions | null = renewalsEnabled
    ? {
        openOfferFor: (leaseId) => openRenewals.find((o) => o.leaseId === leaseId),
        onOffer: setOfferFor,
        onAnswer: setAnswerFor,
      }
    : null;

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Leases</Title>
      <Text c="gray.7">
        Active leases ending in the next 90 days, soonest first — time to offer a renewal or plan to
        re-let the unit.
      </Text>

      {loadError && (
        <Alert color="red" title="Could not load leases" styles={dangerAlertStyles}>
          {leaseErrorMessage(loadError)}
        </Alert>
      )}

      <Tabs value={tab} onChange={setTab}>
        <Tabs.List>
          {BUCKETS.map((bucket) => (
            <Tabs.Tab key={bucket.value} value={bucket.value}>
              {bucket.label} ({expiring.filter((l) => bucket.test(l.daysUntilEnd ?? 0)).length})
            </Tabs.Tab>
          ))}
          <Tabs.Tab value="not-recorded">Term not recorded ({notRecorded.length})</Tabs.Tab>
          {renewalsEnabled && (
            <Tabs.Tab value="renewals">Renewal offers ({openRenewals.length} open)</Tabs.Tab>
          )}
        </Tabs.List>

        {BUCKETS.map((bucket) => (
          <Tabs.Panel key={bucket.value} value={bucket.value} pt="md">
            <ExpiringTable
              leases={expiring.filter((l) => bucket.test(l.daysUntilEnd ?? 0))}
              renewal={renewalActions}
            />
          </Tabs.Panel>
        ))}

        <Tabs.Panel value="not-recorded" pt="md">
          <Stack gap="sm">
            <Text size="sm" c="gray.7">
              These leases started before Trellis recorded lease terms. Record each one's real term
              so it appears in the lists above — nothing here is guessed.
            </Text>
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Unit</Table.Th>
                  <Table.Th>Property</Table.Th>
                  <Table.Th>Started</Table.Th>
                  <Table.Th>Started from</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {notRecorded.map((lease) => (
                  <Table.Tr key={lease.leaseId}>
                    <Table.Td>{lease.unitLabel}</Table.Td>
                    <Table.Td>{lease.propertyAddress}</Table.Td>
                    <Table.Td>{lease.startDate}</Table.Td>
                    <Table.Td>{sourceLabel(lease.source)}</Table.Td>
                    <Table.Td>
                      <Button size="xs" variant="outline" onClick={() => setTermFor(lease)}>
                        Record term
                      </Button>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
            {notRecordedQuery.isSuccess && notRecorded.length === 0 && (
              <Text c="gray.7">Every active lease has its term recorded.</Text>
            )}
          </Stack>
        </Tabs.Panel>
        {renewalsEnabled && (
          <Tabs.Panel value="renewals" pt="md">
            <RenewalsTable offers={renewals} currency={currency} onAnswer={setAnswerFor} />
          </Tabs.Panel>
        )}
      </Tabs>

      <Modal opened={offerFor !== null} onClose={() => setOfferFor(null)} title="Offer a renewal">
        {offerFor && <OfferRenewalForm lease={offerFor} onDone={() => setOfferFor(null)} />}
      </Modal>

      <Modal
        opened={answerFor !== null}
        onClose={() => setAnswerFor(null)}
        title="Record the lessee's answer"
      >
        {answerFor && (
          <RecordRenewalAnswerForm offer={answerFor} onDone={() => setAnswerFor(null)} />
        )}
      </Modal>

      <Modal opened={termFor !== null} onClose={() => setTermFor(null)} title="Record lease term">
        {termFor && (
          <RecordTermForm
            lease={termFor}
            onDone={() => {
              setTermFor(null);
              queryClient.invalidateQueries({ queryKey: ["leases"] });
            }}
          />
        )}
      </Modal>
    </Stack>
  );
}

function ExpiringTable({ leases, renewal }: { leases: Lease[]; renewal: RenewalActions | null }) {
  if (leases.length === 0) {
    return <Text c="gray.7">No leases in this range.</Text>;
  }

  return (
    <Table striped highlightOnHover>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Unit</Table.Th>
          <Table.Th>Property</Table.Th>
          <Table.Th>Started</Table.Th>
          <Table.Th>Ends</Table.Th>
          <Table.Th>Time left</Table.Th>
          <Table.Th>Started from</Table.Th>
          {renewal && <Table.Th>Renewal</Table.Th>}
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {leases.map((lease) => (
          <Table.Tr key={lease.leaseId}>
            <Table.Td>{lease.unitLabel}</Table.Td>
            <Table.Td>{lease.propertyAddress}</Table.Td>
            <Table.Td>{lease.startDate}</Table.Td>
            <Table.Td>{lease.endDate}</Table.Td>
            <Table.Td>
              {(lease.daysUntilEnd ?? 0) < 0 ? (
                <Badge color="red.8">{daysLabel(lease.daysUntilEnd)}</Badge>
              ) : (
                daysLabel(lease.daysUntilEnd)
              )}
            </Table.Td>
            <Table.Td>{sourceLabel(lease.source)}</Table.Td>
            {renewal && (
              <Table.Td>
                <RenewalCell lease={lease} renewal={renewal} />
              </Table.Td>
            )}
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

function RenewalCell({ lease, renewal }: { lease: Lease; renewal: RenewalActions }) {
  const open = renewal.openOfferFor(lease.leaseId);
  if (!open) {
    return (
      <Button size="xs" variant="outline" onClick={() => renewal.onOffer(lease)}>
        Offer renewal
      </Button>
    );
  }

  return (
    <Stack gap={4} align="flex-start">
      <Badge color={open.respondByPassed ? "red.8" : "blue.8"}>
        {open.respondByPassed ? "Reply overdue" : `Offered · reply by ${open.respondBy}`}
      </Badge>
      <Button size="xs" variant="subtle" onClick={() => renewal.onAnswer(open)}>
        Record answer
      </Button>
    </Stack>
  );
}

function RenewalsTable({
  offers,
  currency,
  onAnswer,
}: {
  offers: RenewalOffer[];
  currency: string;
  onAnswer: (offer: RenewalOffer) => void;
}) {
  if (offers.length === 0) {
    return <Text c="gray.7">No renewal offers yet. Offer one from a lease above.</Text>;
  }

  return (
    <Table striped highlightOnHover>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Unit</Table.Th>
          <Table.Th>Offered</Table.Th>
          <Table.Th>New rent</Table.Th>
          <Table.Th>Reply by</Table.Th>
          <Table.Th>Answer</Table.Th>
          <Table.Th />
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {offers.map((offer) => {
          const status = RENEWAL_STATUS[offer.status ?? ""] ?? {
            label: offer.status,
            color: "gray.8",
          };
          return (
            <Table.Tr key={offer.renewalOfferId}>
              <Table.Td>
                {offer.unitLabel}
                <Text size="xs" c="gray.7">
                  {offer.propertyAddress}
                </Text>
              </Table.Td>
              <Table.Td>
                {offer.proposedTerm === "FIXED" ? `To ${offer.proposedEndDate}` : "Month-to-month"}
              </Table.Td>
              <Table.Td>
                {offer.proposedRentAmount != null
                  ? `${formatCurrency(offer.proposedRentAmount, currency)} from ${offer.proposedRentFrom}`
                  : "Unchanged"}
              </Table.Td>
              <Table.Td>{offer.respondBy}</Table.Td>
              <Table.Td>
                <Badge color={offer.respondByPassed ? "red.8" : status.color}>
                  {offer.respondByPassed ? "Reply overdue" : status.label}
                </Badge>
                {offer.outcomeOn && (
                  <Text size="xs" c="gray.7">
                    {offer.outcomeOn}
                    {offer.declineReason ? ` · ${offer.declineReason}` : ""}
                    {offer.rentChangeApplied === false ? " · no rent schedule to change" : ""}
                  </Text>
                )}
              </Table.Td>
              <Table.Td>
                {offer.status === "OFFERED" && (
                  <Button size="xs" variant="subtle" onClick={() => onAnswer(offer)}>
                    Record answer
                  </Button>
                )}
              </Table.Td>
            </Table.Tr>
          );
        })}
      </Table.Tbody>
    </Table>
  );
}

function RecordTermForm({ lease, onDone }: { lease: Lease; onDone: () => void }) {
  const [termType, setTermType] = useState<string | null>("FIXED");
  const [endDate, setEndDate] = useState("");
  const fixed = termType === "FIXED";
  const endBeforeStart = fixed && endDate !== "" && endDate <= (lease.startDate ?? "");

  const mutation = useMutation({
    mutationFn: () =>
      leasesApi.recordTerm(lease.leaseId!, {
        termType: termType!,
        endDate: fixed ? endDate : null,
        rowVersion: lease.rowVersion!,
      }),
    onSuccess: onDone,
  });

  return (
    <Stack gap="md">
      <Text size="sm">
        {lease.unitLabel}, {lease.propertyAddress} — started {lease.startDate}. Currently:{" "}
        {termLabel(lease.termType)}.
      </Text>
      {mutation.isError && (
        <Alert color="red" title="Could not record the term" styles={dangerAlertStyles}>
          {leaseErrorMessage(mutation.error)}
        </Alert>
      )}
      <Select
        label="Lease term"
        data={[
          { value: "FIXED", label: "Fixed term" },
          { value: "MONTH_TO_MONTH", label: "Month-to-month" },
        ]}
        value={termType}
        onChange={setTermType}
        allowDeselect={false}
      />
      {fixed && (
        <TextInput
          label="Lease ends on"
          type="date"
          min={lease.startDate ?? undefined}
          value={endDate}
          onChange={(event) => setEndDate(event.currentTarget.value)}
          error={endBeforeStart ? "The end date must be after the start date." : undefined}
        />
      )}
      <Button
        disabled={(fixed && (endDate === "" || endBeforeStart)) || !termType}
        loading={mutation.isPending}
        onClick={() => mutation.mutate()}
      >
        Save term
      </Button>
    </Stack>
  );
}
