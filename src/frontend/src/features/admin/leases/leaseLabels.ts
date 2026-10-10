import { TrellisApiError } from "../../../api/client";

/** BDR-LSE-011's fixed list, in the order staff are likely to need them. Mirrors the server's LeaseEndRules. */
export const END_REASON_OPTIONS = [
  { value: "NON_RENEWAL", label: "Not renewed" },
  { value: "LESSEE_BREACH", label: "Lessee breach" },
  { value: "MUTUAL_AGREEMENT", label: "Mutual agreement" },
  { value: "PROPERTY_SALE", label: "Property sold" },
  { value: "RENOVATION", label: "Renovation" },
  { value: "OTHER", label: "Other (add a note)" },
];

export const ENDED_BY_OPTIONS = [
  { value: "LESSEE", label: "The lessee" },
  { value: "PM_COMPANY", label: "Us (the property manager)" },
];

const END_REASON_LABEL: Record<string, string> = {
  ...Object.fromEntries(END_REASON_OPTIONS.map((o) => [o.value, o.label])),
  OTHER: "Other",
  NOT_RECORDED: "Not recorded",
};

export const endReasonLabel = (reason?: string | null) =>
  reason ? (END_REASON_LABEL[reason] ?? reason) : "—";

const TERM_LABEL: Record<string, string> = {
  FIXED: "Fixed term",
  MONTH_TO_MONTH: "Month-to-month",
  NOT_RECORDED: "Term not recorded",
};

export const termLabel = (termType?: string | null) =>
  termType ? (TERM_LABEL[termType] ?? termType) : "—";

/** The lease error codes any lease-touching screen can hit, in plain words. */
export function leaseErrorMessage(error: unknown): string {
  if (error instanceof TrellisApiError) {
    switch (error.code) {
      case "TRL-E-AST-3003":
        return "That unit already has an active lease.";
      case "TRL-E-AST-3004":
        return "Check the lease term: a fixed term needs an end date after the start date.";
      case "TRL-E-AST-3005":
        return "Check the end date (not before the lease started) and the reason (Other needs a note).";
      case "TRL-E-RENT-12003":
        return "This unit has no lease yet, so set the lease term — an end date, or month-to-month.";
      case "TRL-E-RENT-12004":
        return "Ending this schedule ends the tenancy, so say when and why it ended.";
      default:
        return error.message;
    }
  }
  return "Something went wrong.";
}
