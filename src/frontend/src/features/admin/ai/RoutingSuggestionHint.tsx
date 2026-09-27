import { Text } from "@mantine/core";

interface RoutingSuggestionHintProps {
  departmentName?: string | null;
  vendorCategory?: string | null;
  confidence?: number | null;
}

/** BDR-AI-003/005 — a plain-text confidence hint, never presented as authoritative: the human still picks the department themselves. */
export function RoutingSuggestionHint({
  departmentName,
  vendorCategory,
  confidence,
}: RoutingSuggestionHintProps) {
  const target = departmentName ?? (vendorCategory ? `a ${vendorCategory} vendor` : null);
  if (!target) {
    return null;
  }

  const confidencePct = confidence != null ? Math.round(confidence * 100) : null;

  return (
    <Text c="blue.8" size="sm">
      AI suggests: {target}
      {confidencePct != null && ` (${confidencePct}% confidence)`}
    </Text>
  );
}
