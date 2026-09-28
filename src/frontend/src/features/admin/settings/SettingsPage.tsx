import { Stack, Title } from "@mantine/core";
import { CustomDomainCard } from "./CustomDomainCard";
import { NotificationsCard } from "./NotificationsCard";

/** Tenant-level settings (TENANT_ADMIN). */
export function SettingsPage() {
  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Settings</Title>
      <NotificationsCard />
      <CustomDomainCard />
    </Stack>
  );
}
