import { Badge, Text } from "@mantine/core";

// Custom hex shades, not Mantine palette names: white text on Mantine's own orange.9 measures ~4.3:1
// and on yellow far lower, both under WCAG AA's 4.5:1 (BDR-NFR-007). #b34700 and red.9 clear it.
const statusStyle: Record<string, { label: string; color: string }> = {
  CURRENT: { label: "Current", color: "success.8" },
  OVERDUE: { label: "Overdue", color: "#b34700" },
  IN_ARREARS: { label: "In arrears", color: "red.9" },
};

/** BDR-RENT-003/005's trio, worded plainly — never "balance", "outstanding funds", or anything implying Trellis holds money (BDR-RENT-006). */
export function RentStatusBadge({ status }: { status?: string | null }) {
  if (!status) {
    return <Badge color="gray.8">Ended</Badge>;
  }
  const style = statusStyle[status] ?? { label: status, color: "gray.8" };
  return <Badge color={style.color}>{style.label}</Badge>;
}

/** BDR-RENT-006 — the standing statement on every rent screen. */
export function RentDisclaimer() {
  return (
    <Text size="sm" c="gray.8">
      Trellis records payments made outside the platform. It does not process or hold funds.
    </Text>
  );
}
