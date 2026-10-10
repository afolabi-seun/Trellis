import { Alert, Button, Modal, Select, Stack, Text, TextInput, Textarea } from "@mantine/core";
import { useState } from "react";
import { dangerAlertStyles, dangerButtonStyles } from "../../../theme/dangerStyles";
import { END_REASON_OPTIONS, ENDED_BY_OPTIONS, leaseErrorMessage } from "./leaseLabels";

export interface EndLeaseValues {
  effectiveDate: string;
  reason: string;
  endedBy: string;
  note: string | null;
}

interface EndLeaseModalProps {
  opened: boolean;
  onClose: () => void;
  onConfirm: (values: EndLeaseValues) => void;
  loading: boolean;
  error: unknown;
  /** What ending this does beyond the lease itself — e.g. "and stops tracking rent". */
  description: string;
  confirmLabel?: string;
}

const today = () => new Date().toISOString().slice(0, 10);

/**
 * BDR-LSE-011 — the one form every "end a lease" path uses (Leasing's End lease, and ending a
 * manually started rent schedule), so the reason list and its rules can't drift between them.
 * The date can't be in the future: ending a lease vacates its unit from that date.
 */
export function EndLeaseModal({ opened, onClose, ...formProps }: EndLeaseModalProps) {
  // The form lives inside the Modal, which unmounts it on close, so every opening starts blank.
  return (
    <Modal opened={opened} onClose={onClose} title="End lease">
      <EndLeaseForm {...formProps} />
    </Modal>
  );
}

function EndLeaseForm({
  onConfirm,
  loading,
  error,
  description,
  confirmLabel = "End lease",
}: Omit<EndLeaseModalProps, "opened" | "onClose">) {
  const [effectiveDate, setEffectiveDate] = useState("");
  const [reason, setReason] = useState<string | null>(null);
  const [endedBy, setEndedBy] = useState<string | null>(null);
  const [note, setNote] = useState("");

  const futureDate = effectiveDate !== "" && effectiveDate > today();
  const noteMissing = reason === "OTHER" && note.trim() === "";
  const ready = effectiveDate !== "" && !futureDate && reason && endedBy && !noteMissing;

  return (
    <Stack gap="md">
      <Text size="sm">{description} It cannot be undone.</Text>
      {Boolean(error) && (
        <Alert color="red" title="Could not end the lease" styles={dangerAlertStyles}>
          {leaseErrorMessage(error)}
        </Alert>
      )}
      <TextInput
        label="Lease ended on"
        type="date"
        max={today()}
        value={effectiveDate}
        onChange={(event) => setEffectiveDate(event.currentTarget.value)}
        error={futureDate ? "The end date can't be in the future." : undefined}
      />
      <Select
        label="Why did it end?"
        data={END_REASON_OPTIONS}
        value={reason}
        onChange={setReason}
      />
      <Select label="Who ended it?" data={ENDED_BY_OPTIONS} value={endedBy} onChange={setEndedBy} />
      <Textarea
        label={reason === "OTHER" ? "Note" : "Note (optional)"}
        maxLength={500}
        value={note}
        onChange={(event) => setNote(event.currentTarget.value)}
        error={noteMissing ? "Add a note when the reason is Other." : undefined}
      />
      <Button
        styles={dangerButtonStyles}
        disabled={!ready}
        loading={loading}
        onClick={() =>
          onConfirm({
            effectiveDate,
            reason: reason!,
            endedBy: endedBy!,
            note: note.trim() === "" ? null : note.trim(),
          })
        }
      >
        {confirmLabel}
      </Button>
    </Stack>
  );
}
