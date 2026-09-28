import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Badge,
  Button,
  Card,
  Code,
  Group,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { tenantDomainsApi } from "../../../api/settings";
import {
  dangerAlertStyles,
  dangerButtonStyles,
  infoAlertStyles,
} from "../../../theme/dangerStyles";

const domainSchema = z.object({
  domain: z
    .string()
    .min(1, "Enter a domain.")
    .regex(/^[^\s/:]+\.[^\s/:]+$/, "Enter a bare domain like listings.example.com."),
});
type DomainFormValues = z.infer<typeof domainSchema>;

const statusColor: Record<string, string> = {
  VERIFIED: "success.8",
  PENDING_VERIFICATION: "blue.8",
  DISABLED: "gray.8",
};

function messageFor(error: unknown): string {
  if (error instanceof TrellisApiError) {
    if (error.code === "TRL-E-TEN-2004")
      return "That isn't a valid domain, or it belongs to the platform itself.";
    if (error.code === "TRL-E-TEN-2005")
      return "We couldn't find the DNS record yet. DNS changes can take a while to propagate — try again shortly.";
    return error.message;
  }
  return "Something went wrong.";
}

/** A tenant's own domain for its public pages (ENTERPRISE). Registering proves nothing: the domain only works once DNS shows the tenant controls it. */
export function CustomDomainCard() {
  const queryClient = useQueryClient();
  const domainsQuery = useQuery({
    queryKey: ["settings", "domains"],
    queryFn: tenantDomainsApi.list,
  });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["settings", "domains"] });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<DomainFormValues>({ resolver: zodResolver(domainSchema) });

  const addMutation = useMutation({
    mutationFn: (values: DomainFormValues) => tenantDomainsApi.add(values.domain),
    onSuccess: () => {
      refresh();
      reset();
    },
  });
  const verifyMutation = useMutation({ mutationFn: tenantDomainsApi.verify, onSuccess: refresh });
  const removeMutation = useMutation({ mutationFn: tenantDomainsApi.remove, onSuccess: refresh });

  const notEnterprise =
    addMutation.error instanceof TrellisApiError && addMutation.error.code === "TRL-E-TEN-2002";
  const actionError = verifyMutation.error ?? removeMutation.error;

  return (
    <Card withBorder padding="lg">
      <Stack gap="md">
        <Title order={3}>Custom domain</Title>
        <Text size="sm" c="gray.8">
          Serve your public vacancy listings from your own domain. Add the domain, publish the DNS
          record shown below, then verify it.
        </Text>

        {notEnterprise && (
          <Alert
            color="blue"
            title="Custom domains aren't enabled for your plan"
            styles={infoAlertStyles}
          >
            Custom domains are part of the Enterprise plan. Contact your Trellis administrator to
            upgrade.
          </Alert>
        )}
        {addMutation.isError && !notEnterprise && (
          <Alert color="red" title="Could not add domain" styles={dangerAlertStyles}>
            {messageFor(addMutation.error)}
          </Alert>
        )}
        {actionError && (
          <Alert color="red" title="Something went wrong" styles={dangerAlertStyles}>
            {messageFor(actionError)}
          </Alert>
        )}

        <form onSubmit={handleSubmit((values) => addMutation.mutate(values))}>
          <Group align="flex-end">
            <TextInput
              label="Domain"
              placeholder="listings.example.com"
              error={errors.domain?.message}
              style={{ flex: 1 }}
              {...register("domain")}
            />
            <Button type="submit" loading={addMutation.isPending}>
              Add domain
            </Button>
          </Group>
        </form>

        {domainsQuery.data && domainsQuery.data.length > 0 && (
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Domain</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {domainsQuery.data.map((domain) => (
                <Table.Tr key={domain.tenantDomainId}>
                  <Table.Td>
                    <Text>{domain.domain}</Text>
                    {domain.verificationRecordName && (
                      <Stack gap={4} mt={4}>
                        <Text size="sm" c="gray.8">
                          Publish this DNS TXT record, then choose Verify:
                        </Text>
                        <Text size="sm">
                          Name: <Code>{domain.verificationRecordName}</Code>
                        </Text>
                        <Text size="sm">
                          Value: <Code>{domain.verificationRecordValue}</Code>
                        </Text>
                      </Stack>
                    )}
                  </Table.Td>
                  <Table.Td>
                    <Badge color={statusColor[domain.status ?? ""] ?? "gray.8"}>
                      {domain.status === "PENDING_VERIFICATION" ? "Pending" : domain.status}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Group gap="xs" justify="flex-end">
                      {domain.status === "PENDING_VERIFICATION" && (
                        <Button
                          size="xs"
                          loading={
                            verifyMutation.isPending &&
                            verifyMutation.variables === domain.tenantDomainId
                          }
                          onClick={() => verifyMutation.mutate(domain.tenantDomainId!)}
                        >
                          Verify
                        </Button>
                      )}
                      <Button
                        size="xs"
                        variant="subtle"
                        styles={dangerButtonStyles}
                        onClick={() => {
                          if (
                            confirm(`Remove ${domain.domain}? Links using it will stop working.`)
                          ) {
                            removeMutation.mutate(domain.tenantDomainId!);
                          }
                        }}
                      >
                        Remove
                      </Button>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Stack>
    </Card>
  );
}
