import { Alert, Button, Card, Checkbox, Stack, Text, Title } from "@mantine/core";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { TrellisApiError } from "../../../api/client";
import { tenantSettingsApi } from "../../../api/settings";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { useTenantSettings } from "./useTenantSettings";

const ROLE_OPTIONS = [
  { value: "TENANT_ADMIN", label: "Tenant admins" },
  { value: "DEPARTMENT_STAFF", label: "Department staff" },
];

interface Choice {
  applicationSubmittedRoles: string[];
  rentArrearsRoles: string[];
}

/** Who is told about new rental applications and about rent arrears (BDR-RENT-004's "configured staff"). Defaults to both staff roles. */
export function NotificationsCard() {
  const queryClient = useQueryClient();
  const settingsQuery = useTenantSettings();
  // The user's unsaved edits; until they touch a list it simply mirrors what the server has.
  const [edits, setEdits] = useState<Partial<Choice>>({});

  const saved = settingsQuery.data?.notifications;
  const current: Choice = {
    applicationSubmittedRoles:
      edits.applicationSubmittedRoles ?? saved?.applicationSubmittedRoles ?? [],
    rentArrearsRoles: edits.rentArrearsRoles ?? saved?.rentArrearsRoles ?? [],
  };
  const dirty =
    edits.applicationSubmittedRoles !== undefined || edits.rentArrearsRoles !== undefined;
  const invalid =
    current.applicationSubmittedRoles.length === 0 || current.rentArrearsRoles.length === 0;

  const saveMutation = useMutation({
    mutationFn: () => tenantSettingsApi.updateNotifications(current),
    onSuccess: (data) => {
      queryClient.setQueryData(["settings", "tenant"], data);
      setEdits({});
    },
  });

  return (
    <Card withBorder padding="lg">
      <Stack gap="md">
        <Title order={3}>Notifications</Title>
        <Text size="sm" c="gray.8">
          Choose who receives these alerts. At least one role must be selected for each.
        </Text>

        {saveMutation.isError && (
          <Alert color="red" title="Could not save" styles={dangerAlertStyles}>
            {saveMutation.error instanceof TrellisApiError && saveMutation.error.status === 409
              ? "Someone else changed these settings. Reload and try again."
              : "Something went wrong."}
          </Alert>
        )}
        {saveMutation.isSuccess && !dirty && (
          <Text role="status" size="sm" fw={600} c="success.8">
            Notification settings saved
          </Text>
        )}

        <Checkbox.Group
          label="New rental applications"
          description="Sent when a prospective tenant submits an application."
          value={current.applicationSubmittedRoles}
          onChange={(value) => setEdits((e) => ({ ...e, applicationSubmittedRoles: value }))}
        >
          <Stack gap="xs" mt="xs">
            {ROLE_OPTIONS.map((o) => (
              <Checkbox key={o.value} value={o.value} label={o.label} />
            ))}
          </Stack>
        </Checkbox.Group>

        <Checkbox.Group
          label="Rent arrears"
          description="Sent when a rent schedule has an unpaid period past its grace period."
          value={current.rentArrearsRoles}
          onChange={(value) => setEdits((e) => ({ ...e, rentArrearsRoles: value }))}
        >
          <Stack gap="xs" mt="xs">
            {ROLE_OPTIONS.map((o) => (
              <Checkbox key={o.value} value={o.value} label={o.label} />
            ))}
          </Stack>
        </Checkbox.Group>

        <Button
          onClick={() => saveMutation.mutate()}
          loading={saveMutation.isPending}
          disabled={invalid}
          style={{ alignSelf: "flex-start" }}
        >
          Save notification settings
        </Button>
      </Stack>
    </Card>
  );
}
