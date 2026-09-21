import { zodResolver } from "@hookform/resolvers/zod";
import { Alert, Button, Center, Paper, PasswordInput, Stack, Text, Title } from "@mantine/core";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { authApi } from "../../api/auth";
import { TrellisApiError } from "../../api/client";

const schema = z
  .object({
    newPassword: z.string().min(12, "Password must be at least 12 characters."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

type FormValues = z.infer<typeof schema>;

/** The token arrives as a query param on the link `AuthService.IssuePasswordSetLinkAsync` sends — `{AppOptions.BaseUrl}/reset-password?token=...`. */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const resetMutation = useMutation({
    mutationFn: (newPassword: string) => authApi.resetPassword({ token: token ?? "", newPassword }),
  });

  if (!token) {
    return (
      <Center h="100vh" bg="gray.0">
        <Paper withBorder shadow="md" p="xl" radius="md" w={400}>
          <Alert color="red" title="Invalid link">
            This password reset link is missing its token. Request a new one.
          </Alert>
        </Paper>
      </Center>
    );
  }

  return (
    <Center h="100vh" bg="gray.0">
      <Paper withBorder shadow="md" p="xl" radius="md" w={400}>
        <Stack gap="lg">
          <Title order={2}>Set a new password</Title>

          {resetMutation.isSuccess ? (
            <>
              <Text>Your password has been reset. All other sessions have been signed out.</Text>
              <Button component={Link} to="/login" fullWidth>
                Sign in
              </Button>
            </>
          ) : (
            <>
              {resetMutation.isError && (
                <Alert color="red" title="Could not reset password">
                  {resetMutation.error instanceof TrellisApiError
                    ? resetMutation.error.message
                    : "This link may be invalid or expired. Request a new one."}
                </Alert>
              )}

              <form onSubmit={handleSubmit((values) => resetMutation.mutate(values.newPassword))}>
                <Stack gap="md">
                  <PasswordInput
                    label="New password"
                    autoComplete="new-password"
                    error={errors.newPassword?.message}
                    {...register("newPassword")}
                  />
                  <PasswordInput
                    label="Confirm new password"
                    autoComplete="new-password"
                    error={errors.confirmPassword?.message}
                    {...register("confirmPassword")}
                  />
                  <Button type="submit" loading={resetMutation.isPending} fullWidth>
                    Reset password
                  </Button>
                </Stack>
              </form>
            </>
          )}
        </Stack>
      </Paper>
    </Center>
  );
}
