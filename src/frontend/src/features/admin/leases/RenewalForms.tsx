import {
  Alert,
  Button,
  Checkbox,
  NumberInput,
  Select,
  Stack,
  Text,
  TextInput,
  Textarea,
} from "@mantine/core";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { renewalOffersApi } from "../../../api/leasing";
import type { components } from "../../../api/schema";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { useTenantSettings } from "../settings/useTenantSettings";
import { renewalErrorMessage, termLabel } from "./leaseLabels";

type Lease = components["schemas"]["LeaseResponse"];
type RenewalOffer = components["schemas"]["RenewalOfferResponse"];

const today = () => new Date().toISOString().slice(0, 10);

function addDays(isoDate: string, days: number) {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function addYears(isoDate: string, years: number) {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCFullYear(date.getUTCFullYear() + years);
  return date.toISOString().slice(0, 10);
}

/** BDR-LSE-010 — the terms offered to the lessee. The current term's end anchors every default. */
export function OfferRenewalForm({ lease, onDone }: { lease: Lease; onDone: () => void }) {
  const queryClient = useQueryClient();
  const anchor = lease.endDate ?? today();
  const [term, setTerm] = useState<string | null>("FIXED");
  const [endDate, setEndDate] = useState(addYears(anchor, 1));
  const [changeRent, setChangeRent] = useState(false);
  const [rentAmount, setRentAmount] = useState<number | string>("");
  const [rentFrom, setRentFrom] = useState(addDays(anchor, 1));
  const [respondBy, setRespondBy] = useState(addDays(today(), 14));

  const fixed = term === "FIXED";
  const endNotAfterTerm =
    fixed && endDate !== "" && endDate <= (lease.endDate ?? lease.startDate ?? "");
  const rentMissing =
    changeRent && (typeof rentAmount !== "number" || rentAmount <= 0 || !rentFrom);
  const respondByPast = respondBy !== "" && respondBy < today();
  const ready =
    term &&
    (!fixed || (endDate !== "" && !endNotAfterTerm)) &&
    !rentMissing &&
    respondBy !== "" &&
    !respondByPast;

  const mutation = useMutation({
    mutationFn: () =>
      renewalOffersApi.create({
        leaseId: lease.leaseId!,
        proposedTerm: term!,
        proposedEndDate: fixed ? endDate : null,
        proposedRentAmount: changeRent ? (rentAmount as number) : null,
        proposedRentFrom: changeRent ? rentFrom : null,
        respondBy,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leases"] });
      onDone();
    },
  });

  return (
    <Stack gap="md">
      <Text size="sm">
        {lease.unitLabel}, {lease.propertyAddress} — {termLabel(lease.termType)}
        {lease.endDate ? `, ending ${lease.endDate}` : ""}.
      </Text>
      {mutation.isError && (
        <Alert color="red" title="Could not record the offer" styles={dangerAlertStyles}>
          {renewalErrorMessage(mutation.error)}
        </Alert>
      )}
      <Select
        label="Renew for"
        data={[
          { value: "FIXED", label: "A fixed term" },
          { value: "MONTH_TO_MONTH", label: "Month-to-month" },
        ]}
        value={term}
        onChange={setTerm}
        allowDeselect={false}
      />
      {fixed && (
        <TextInput
          label="New end date"
          type="date"
          value={endDate}
          onChange={(event) => setEndDate(event.currentTarget.value)}
          error={endNotAfterTerm ? "The renewal must end after the current term." : undefined}
        />
      )}
      <Checkbox
        label="Change the rent"
        checked={changeRent}
        onChange={(event) => setChangeRent(event.currentTarget.checked)}
      />
      {changeRent && (
        <>
          <NumberInput
            label="New rent per period"
            min={0}
            value={rentAmount}
            onChange={setRentAmount}
          />
          <TextInput
            label="New rent applies from"
            description="Earlier periods keep their current rent."
            type="date"
            value={rentFrom}
            onChange={(event) => setRentFrom(event.currentTarget.value)}
          />
        </>
      )}
      <TextInput
        label="Lessee to respond by"
        type="date"
        min={today()}
        value={respondBy}
        onChange={(event) => setRespondBy(event.currentTarget.value)}
        error={respondByPast ? "The respond-by date can't be in the past." : undefined}
      />
      <Button disabled={!ready} loading={mutation.isPending} onClick={() => mutation.mutate()}>
        Record offer
      </Button>
    </Stack>
  );
}

/** BDR-LSE-010 — what the lessee said: accepted (extends the lease), declined (with a reason), or no answer by the deadline. */
export function RecordRenewalAnswerForm({
  offer,
  onDone,
}: {
  offer: RenewalOffer;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const settingsQuery = useTenantSettings();
  const [answer, setAnswer] = useState<string | null>(null);
  const [respondedOn, setRespondedOn] = useState(today());
  const [reason, setReason] = useState<string | null>(null);
  const [note, setNote] = useState("");

  const reasonOptions = [...(settingsQuery.data?.renewalDeclineReasons ?? []), "Other"];
  const answerOptions = [
    { value: "ACCEPTED", label: "Accepted — extend the lease" },
    { value: "DECLINED", label: "Declined" },
    ...(offer.respondByPassed ? [{ value: "EXPIRED", label: "No answer by the deadline" }] : []),
  ];
  const futureDate = respondedOn > today();
  const noteMissing = answer === "DECLINED" && reason === "Other" && note.trim() === "";
  const ready =
    answer === "EXPIRED" ||
    (answer === "ACCEPTED" && respondedOn !== "" && !futureDate) ||
    (answer === "DECLINED" && respondedOn !== "" && !futureDate && reason && !noteMissing);

  const mutation = useMutation({
    mutationFn: () => {
      const id = offer.renewalOfferId!;
      if (answer === "ACCEPTED") return renewalOffersApi.accept(id, respondedOn);
      if (answer === "DECLINED")
        return renewalOffersApi.decline(id, respondedOn, reason!, note.trim() || null);
      return renewalOffersApi.expire(id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leases"] });
      queryClient.invalidateQueries({ queryKey: ["rent"] });
      onDone();
    },
  });

  return (
    <Stack gap="md">
      <Text size="sm">
        {offer.unitLabel}, {offer.propertyAddress} — offered{" "}
        {offer.proposedTerm === "FIXED" ? `to ${offer.proposedEndDate}` : "month-to-month"}, reply
        by {offer.respondBy}.
      </Text>
      {mutation.isError && (
        <Alert color="red" title="Could not record the answer" styles={dangerAlertStyles}>
          {renewalErrorMessage(mutation.error)}
        </Alert>
      )}
      <Select label="Answer" data={answerOptions} value={answer} onChange={setAnswer} />
      {(answer === "ACCEPTED" || answer === "DECLINED") && (
        <TextInput
          label="Answered on"
          type="date"
          max={today()}
          value={respondedOn}
          onChange={(event) => setRespondedOn(event.currentTarget.value)}
          error={futureDate ? "The answer date can't be in the future." : undefined}
        />
      )}
      {answer === "DECLINED" && (
        <>
          <Select label="Why?" data={reasonOptions} value={reason} onChange={setReason} />
          <Textarea
            label={reason === "Other" ? "Note" : "Note (optional)"}
            maxLength={500}
            value={note}
            onChange={(event) => setNote(event.currentTarget.value)}
            error={noteMissing ? "Add a note when the reason is Other." : undefined}
          />
        </>
      )}
      <Button disabled={!ready} loading={mutation.isPending} onClick={() => mutation.mutate()}>
        Save answer
      </Button>
    </Stack>
  );
}
