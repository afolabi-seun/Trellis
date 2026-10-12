import type { components } from "../../../api/schema";

type PaymentTimeliness = components["schemas"]["PaymentTimelinessDto"];

/**
 * BDR-RENT-007 in words. "No periods past grace yet" rather than a 0% or 100% nobody measured, and
 * days late only when something was late.
 */
export function describeTimeliness(t?: PaymentTimeliness | null): string {
  if (!t || !t.periodsAssessed || t.onTimeRate == null) return "No periods past grace yet";
  const percent = Math.round(t.onTimeRate * 100);
  const late =
    t.averageDaysLate != null ? ` · late periods averaged ${t.averageDaysLate} days late` : "";
  return `${t.periodsOnTime} of ${t.periodsAssessed} periods paid on time (${percent}%)${late}`;
}

/** The short form for a table cell. */
export function timelinessPercent(t?: PaymentTimeliness | null): string {
  if (!t || !t.periodsAssessed || t.onTimeRate == null) return "—";
  return `${Math.round(t.onTimeRate * 100)}%`;
}
