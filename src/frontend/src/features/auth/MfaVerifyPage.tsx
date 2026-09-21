import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Anchor, Button, Center, Paper, Stack, Text, TextInput, Title } from "@mantine/core";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { authApi } from "../../api/auth";
import { setAccessToken, TrellisApiError } from "../../api/client";
import { decodeAccessToken } from "../../auth/tokenClaims";

const totpSchema = z.object({ code: z.string().length(6, "Enter the 6-digit code.") });
const recoverySchema = z.object({ code: z.string().min(1, "Enter a recovery code.") });

const roleHome: Record<string, string> = {
  SUPER_ADMIN: "/platform",
  TENANT_ADMIN: "/admin",
  DEPARTMENT_STAFF: "/admin",
  VENDOR_USER: "/vendor",
};

/** api_standards.md's `mfa/verify` — one endpoint, two mutually exclusive request shapes; this mirrors that as one page, two modes, not two routes. */
export function MfaVerifyPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [usingRecoveryCode, setUsingRecoveryCode] = useState(false);
  const partialToken = (location.state as { partialToken?: string } | null)?.partialToken;

  const schema = usingRecoveryCode ? recoverySchema : totpSchema;
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<{ code: string }>({ resolver: zodResolver(schema) });

  const verifyMutation = useMutation({
    mutationFn: (code: string) =>
      authApi.verifyMfa(
        usingRecoveryCode ? { partialToken, recoveryCode: code } : { partialToken, totpCode: code },
      ),
    onSuccess: (data) => {
      if (!data.accessToken) {
        return;
      }
      setAccessToken(data.accessToken);
      const claims = decodeAccessToken(data.accessToken);
      navigate(roleHome[claims?.role ?? ""] ?? "/", { replace: true });
    },
  });

  if (!partialToken) {
    return <Navigate to="/login" replace />;
  }

  return (
    <Center h="100vh" bg="gray.0">
      <Paper withBorder shadow="md" p="xl" radius="md" w={400}>
        <Stack gap="lg">
          <div>
            <Title order={2}>Two-factor verification</Title>
            <Text c="dimmed" size="sm">
              {usingRecoveryCode
                ? "Enter one of your recovery codes."
                : "Enter the 6-digit code from your authenticator app."}
            </Text>
          </div>

          {verifyMutation.isError && (
            <Alert color="red" title="Verification failed">
              {verifyMutation.error instanceof TrellisApiError
                ? verifyMutation.error.message
                : "Something went wrong. Please try again."}
            </Alert>
          )}

          <form onSubmit={handleSubmit((values) => verifyMutation.mutate(values.code))}>
            <Stack gap="md">
              <TextInput
                label={usingRecoveryCode ? "Recovery code" : "Authentication code"}
                autoComplete="one-time-code"
                error={errors.code?.message}
                {...register("code")}
              />
              <Anchor
                size="sm"
                onClick={() => {
                  setUsingRecoveryCode((v) => !v);
                  reset();
                }}
              >
                {usingRecoveryCode
                  ? "Use an authenticator code instead"
                  : "Use a recovery code instead"}
              </Anchor>
              <Button type="submit" loading={verifyMutation.isPending} fullWidth>
                Verify
              </Button>
            </Stack>
          </form>
        </Stack>
      </Paper>
    </Center>
  );
}
