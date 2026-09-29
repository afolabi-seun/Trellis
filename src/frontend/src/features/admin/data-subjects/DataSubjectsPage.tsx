import {
  Alert,
  Button,
  Group,
  List,
  Modal,
  PasswordInput,
  Stack,
  Table,
  Text,
  Textarea,
  TextInput,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { dataSubjectsApi } from "../../../api/dataSubjects";
import { TrellisApiError } from "../../../api/client";
import {
  dangerAlertStyles,
  dangerButtonStyles,
  infoAlertStyles,
} from "../../../theme/dangerStyles";

// Found via this page's own axe scan, inside the Modal specifically: Mantine's default Textarea
// input color measures 4.29:1 there, just under the 4.5:1 floor — same Styles API override
// mechanism dangerButtonStyles/dangerAlertStyles use (dangerStyles.ts's own doc comment: a
// variant/color-prop change doesn't reliably fix Mantine's resolved contrast).
const textareaStyles = { input: { color: "var(--mantine-color-gray-9)" } };

function errorMessage(error: unknown): string {
  if (error instanceof TrellisApiError) {
    if (error.code === "TRL-E-TEN-2006") {
      return "You can't erase your own login, or your tenant's last active Tenant Admin.";
    }
    if (error.code === "TRL-E-AUT-1004") {
      return "That password isn't correct.";
    }
    return error.message;
  }
  return "Something went wrong.";
}

/** security_guide.md's POPIA data-subject rights — TENANT_ADMIN only. Export downloads a JSON bundle; erasure is irreversible and gated by a typed email confirmation plus a password re-entry. */
export function DataSubjectsPage() {
  const [subjectEmail, setSubjectEmail] = useState("");
  const [confirmEmail, setConfirmEmail] = useState("");
  const [password, setPassword] = useState("");
  const [reason, setReason] = useState("");
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);

  const exportMutation = useMutation({
    mutationFn: (email: string) => dataSubjectsApi.export({ subjectEmail: email }),
    onSuccess: (data) => {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `data-subject-export-${Date.now()}.json`;
      link.click();
      URL.revokeObjectURL(url);
    },
  });

  const eraseMutation = useMutation({
    mutationFn: () =>
      dataSubjectsApi.erase({
        subjectEmail,
        confirmEmail,
        currentPassword: password,
        reason: reason || null,
      }),
  });

  const closeAndReset = () => {
    closeModal();
    setConfirmEmail("");
    setPassword("");
    setReason("");
    eraseMutation.reset();
  };

  return (
    <Stack gap="lg" p="lg">
      <Title order={2}>Data subjects</Title>

      <Alert color="blue" title="What this covers" styles={infoAlertStyles}>
        Applicants, property owners, and staff logins are matched by email. Retained regardless of
        erasure: the executed lease document itself, rent schedules and payment records (no PII in
        them), and audit records (IDs and hashes only, never the subject's own email or name).
      </Alert>

      <TextInput
        label="Subject email"
        placeholder="someone@example.com"
        value={subjectEmail}
        onChange={(event) => setSubjectEmail(event.currentTarget.value)}
      />

      {exportMutation.isError && (
        <Alert color="red" title="Could not export" styles={dangerAlertStyles}>
          {errorMessage(exportMutation.error)}
        </Alert>
      )}

      <Group>
        <Button
          variant="light"
          disabled={!subjectEmail}
          loading={exportMutation.isPending}
          onClick={() => exportMutation.mutate(subjectEmail)}
        >
          Export data
        </Button>
        <Button
          variant="subtle"
          styles={dangerButtonStyles}
          disabled={!subjectEmail}
          onClick={() => {
            setConfirmEmail("");
            openModal();
          }}
        >
          Erase data
        </Button>
      </Group>

      <Modal opened={modalOpened} onClose={closeAndReset} title="Erase this subject's data">
        <Stack gap="md">
          {!eraseMutation.data && (
            <>
              <Text size="sm">
                This is irreversible. Retype the subject's email to confirm, then your own password.
              </Text>
              {eraseMutation.isError && (
                <Alert color="red" title="Could not erase" styles={dangerAlertStyles}>
                  {errorMessage(eraseMutation.error)}
                </Alert>
              )}
              <TextInput
                label="Retype subject email"
                value={confirmEmail}
                onChange={(event) => setConfirmEmail(event.currentTarget.value)}
              />
              <PasswordInput
                label="Your password"
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
              />
              <Textarea
                label="Reason (optional)"
                styles={textareaStyles}
                value={reason}
                onChange={(event) => setReason(event.currentTarget.value)}
              />
              <Button
                variant="subtle"
                styles={dangerButtonStyles}
                disabled={confirmEmail !== subjectEmail || !password}
                loading={eraseMutation.isPending}
                onClick={() => eraseMutation.mutate()}
              >
                Erase permanently
              </Button>
            </>
          )}
          {eraseMutation.data && (
            <>
              <Alert color="success" title="Erasure complete">
                Every module's own record of this subject has been redacted.
              </Alert>
              <Table striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Module</Table.Th>
                    <Table.Th>Records affected</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {eraseMutation.data.counts?.map((count) => (
                    <Table.Tr key={count.moduleName}>
                      <Table.Td>{count.moduleName}</Table.Td>
                      <Table.Td>{count.recordsAffected}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
              <List size="sm" c="gray.7">
                <List.Item>
                  The executed lease document, rent/payment records, and audit records are retained.
                </List.Item>
              </List>
              <Button onClick={closeAndReset}>Done</Button>
            </>
          )}
        </Stack>
      </Modal>
    </Stack>
  );
}
