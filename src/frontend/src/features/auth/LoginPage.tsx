import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Anchor,
  Button,
  Center,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { z } from "zod";
import { authApi } from "../../api/auth";
import { setAccessToken, TrellisApiError } from "../../api/client";
import { decodeAccessToken } from "../../auth/tokenClaims";

const schema = z.object({
  username: z.string().min(1, "Username is required."),
  password: z.string().min(1, "Password is required."),
});

type FormValues = z.infer<typeof schema>;

const roleHome: Record<string, string> = {
  SUPER_ADMIN: "/platform",
  TENANT_ADMIN: "/admin",
  DEPARTMENT_STAFF: "/admin",
  VENDOR_USER: "/vendor",
};

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const loginMutation = useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      if (data.mfaRequired && data.partialToken) {
        navigate("/mfa-verify", { state: { partialToken: data.partialToken } });
        return;
      }

      if (!data.accessToken) {
        return;
      }

      setAccessToken(data.accessToken);
      const claims = decodeAccessToken(data.accessToken);
      const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname;
      navigate(from ?? roleHome[claims?.role ?? ""] ?? "/", { replace: true });
    },
  });

  return (
    <Center h="100vh" bg="gray.0">
      <Paper withBorder shadow="md" p="xl" radius="md" w={400}>
        <Stack gap="lg">
          <div>
            <Title order={2}>Sign in</Title>
            <Text c="gray.7" size="sm">
              Trellis platform access
            </Text>
          </div>

          {loginMutation.isError && (
            <Alert color="red" title="Sign-in failed">
              {loginMutation.error instanceof TrellisApiError
                ? loginMutation.error.message
                : "Something went wrong. Please try again."}
            </Alert>
          )}

          <form onSubmit={handleSubmit((values) => loginMutation.mutate(values))}>
            <Stack gap="md">
              <TextInput
                label="Username"
                autoComplete="username"
                error={errors.username?.message}
                {...register("username")}
              />
              <PasswordInput
                label="Password"
                autoComplete="current-password"
                error={errors.password?.message}
                {...register("password")}
              />
              <Anchor component={Link} to="/forgot-password" size="sm">
                Forgot password?
              </Anchor>
              <Button type="submit" loading={loginMutation.isPending} fullWidth>
                Sign in
              </Button>
            </Stack>
          </form>
        </Stack>
      </Paper>
    </Center>
  );
}
