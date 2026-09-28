/** The first money-formatting helper in the frontend — Billing is the first module to need one in more than one place (tenant settings, platform revenue view, invoice history). */
export function formatCurrency(amount: number, currency = "USD"): string {
  return new Intl.NumberFormat("en-US", { style: "currency", currency }).format(amount);
}
