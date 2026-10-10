import { Alert, Button, Card, Stack, TagsInput, Text, Title } from "@mantine/core";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { TrellisApiError } from "../../../api/client";
import { tenantSettingsApi } from "../../../api/settings";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { useTenantSettings } from "./useTenantSettings";

const MAX_REASONS = 15;

/** BDR-LSE-010 — the reasons staff choose from when a lessee declines a renewal. "Other" (with a note) is always offered as well. */
export function RenewalDeclineReasonsCard() {
  const queryClient = useQueryClient();
  const settingsQuery = useTenantSettings();
  // Unsaved edits; until the list is touched it mirrors what the server has.
  const [edited, setEdited] = useState<string[] | null>(null);

  const reasons = edited ?? settingsQuery.data?.renewalDeclineReasons ?? [];
  const includesOther = reasons.some((r) => r.trim().toLowerCase() === "other");
  const invalid = reasons.length === 0 || includesOther;

  const saveMutation = useMutation({
    mutationFn: () => tenantSettingsApi.updateRenewalDeclineReasons(reasons),
    onSuccess: (data) => {
      queryClient.setQueryData(["settings", "tenant"], data);
      setEdited(null);
    },
  });

  return (
    <Card withBorder padding="lg">
      <Stack gap="md">
        <Title order={3}>Renewal decline reasons</Title>
        <Text size="sm" c="gray.7">
          The reasons offered when a lessee turns down a renewal. &ldquo;Other&rdquo;, with a note,
          is always offered as well.
        </Text>
        {saveMutation.isError && (
          <Alert color="red" title="Could not save the reasons" styles={dangerAlertStyles}>
            {saveMutation.error instanceof TrellisApiError
              ? saveMutation.error.message
              : "Something went wrong."}
          </Alert>
        )}
        <TagsInput
          label="Reasons"
          description="Press Enter after each one."
          value={reasons}
          onChange={setEdited}
          maxTags={MAX_REASONS}
          splitChars={[]}
          clearable
          error={
            includesOther
              ? "Other is always offered, so it doesn't need to be in the list."
              : reasons.length === 0 && edited !== null
                ? "Keep at least one reason."
                : undefined
          }
        />
        <Button
          disabled={edited === null || invalid}
          loading={saveMutation.isPending}
          onClick={() => saveMutation.mutate()}
          style={{ alignSelf: "flex-start" }}
        >
          Save decline reasons
        </Button>
      </Stack>
    </Card>
  );
}
