import { zodResolver } from "@hookform/resolvers/zod";
import { Anchor, Button, Center, Paper, Stack, Text, TextInput, Title } from "@mantine/core";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { authApi } from "../../api/auth";

const schema = z.object({ username: z.string().min(1, "Username is required.") });
type FormValues = z.infer<typeof schema>;

/**
 * security_guide.md's existence-non-disclosure: the success message is identical whether or not
 * the account exists — this page never branches on that, because the API response doesn't
 * either (`AuthController.ForgotPassword` always returns the same generic 200).
 */
export function ForgotPasswordPage() {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const forgotMutation = useMutation({ mutationFn: authApi.forgotPassword });

  return (
    <Center h="100vh" bg="gray.0">
      <Paper withBorder shadow="md" p="xl" radius="md" w={400}>
        <Stack gap="lg">
          <div>
            <Title order={2}>Reset your password</Title>
            <Text c="gray.7" size="sm">
              Enter your username and we&apos;ll send a reset link if an account exists.
            </Text>
          </div>

          {forgotMutation.isSuccess ? (
            <Text data-testid="forgot-password-success">
              If an account with that username exists, a password reset link has been sent.
            </Text>
          ) : (
            <form onSubmit={handleSubmit((values) => forgotMutation.mutate(values))}>
              <Stack gap="md">
                <TextInput
                  label="Username"
                  autoComplete="username"
                  error={errors.username?.message}
                  {...register("username")}
                />
                <Button type="submit" loading={forgotMutation.isPending} fullWidth>
                  Send reset link
                </Button>
              </Stack>
            </form>
          )}

          <Anchor component={Link} to="/login" size="sm">
            Back to sign in
          </Anchor>
        </Stack>
      </Paper>
    </Center>
  );
}
