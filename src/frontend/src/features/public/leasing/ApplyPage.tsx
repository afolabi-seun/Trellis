import { zodResolver } from "@hookform/resolvers/zod";
import {
  Alert,
  Checkbox,
  Container,
  NumberInput,
  Stack,
  Text,
  TextInput,
  Title,
  Button,
} from "@mantine/core";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Controller, useForm } from "react-hook-form";
import { useParams } from "react-router-dom";
import { z } from "zod";
import { TrellisApiError } from "../../../api/client";
import { publicLeasingApi } from "../../../api/leasing";
import { dangerAlertStyles } from "../../../theme/dangerStyles";
import { formatCurrency } from "../../../utils/formatCurrency";

function isAtLeast18(dob: string): boolean {
  const cutoff = new Date();
  cutoff.setFullYear(cutoff.getFullYear() - 18);
  return new Date(dob) <= cutoff;
}

const applySchema = z.object({
  fullName: z.string().min(1, "Full name is required."),
  phone: z.string().min(1, "Phone is required."),
  dateOfBirth: z
    .string()
    .min(1, "Date of birth is required.")
    .refine(isAtLeast18, "You must be at least 18 years old."),
  currentAddress: z.string().min(1, "Current address is required."),
  employerName: z.string().optional(),
  monthlyIncome: z.number().nonnegative().optional(),
  consentToScreening: z.literal(true, {
    errorMap: () => ({ message: "You must consent to screening to apply." }),
  }),
});
type ApplyFormValues = z.infer<typeof applySchema>;

/** BDR-LSE-002/008 — the secure-link application. The email is read-only and comes from the link; the server never accepts one from this form. */
export function ApplyPage() {
  const { token } = useParams<{ token: string }>();

  const contextQuery = useQuery({
    queryKey: ["public-leasing", "link", token],
    queryFn: () => publicLeasingApi.resolveLink(token!),
    enabled: Boolean(token),
    retry: false,
  });

  const {
    control,
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ApplyFormValues>({ resolver: zodResolver(applySchema) });

  const submitMutation = useMutation({
    mutationFn: (values: ApplyFormValues) =>
      publicLeasingApi.submitApplication(token!, {
        fullName: values.fullName,
        phone: values.phone,
        dateOfBirth: values.dateOfBirth,
        currentAddress: values.currentAddress,
        employerName: values.employerName || null,
        monthlyIncome: values.monthlyIncome ?? null,
        consentToScreening: values.consentToScreening,
      }),
  });

  if (contextQuery.isError) {
    return (
      <Container size="sm" py="xl">
        <Alert
          color="red"
          title="This link isn't valid"
          styles={dangerAlertStyles}
          component="main"
        >
          This application link is invalid, expired, or has already been used. Request a new link
          from the listing page.
        </Alert>
      </Container>
    );
  }

  if (submitMutation.isSuccess) {
    return (
      <Container size="sm" py="xl">
        <Stack gap="md" component="main">
          <Title order={1}>Application received</Title>
          <Text>
            Thank you. Your application has been submitted and the property manager will be in
            touch.
          </Text>
        </Stack>
      </Container>
    );
  }

  const context = contextQuery.data;

  return (
    <Container size="sm" py="xl">
      <Stack gap="lg" component="main">
        <Title order={1}>Rental application</Title>
        {context && (
          <Text>
            {context.unitLabel} ·{" "}
            {formatCurrency(context.rentAmount ?? 0, context.currency ?? "USD")} / month
          </Text>
        )}

        {context && (
          <form onSubmit={handleSubmit((values) => submitMutation.mutate(values))}>
            <Stack gap="md">
              <TextInput label="Email" value={context.applicantEmail ?? ""} readOnly />
              <TextInput
                label="Full name"
                error={errors.fullName?.message}
                {...register("fullName")}
              />
              <TextInput label="Phone" error={errors.phone?.message} {...register("phone")} />
              <TextInput
                label="Date of birth"
                type="date"
                error={errors.dateOfBirth?.message}
                {...register("dateOfBirth")}
              />
              <TextInput
                label="Current address"
                error={errors.currentAddress?.message}
                {...register("currentAddress")}
              />
              <TextInput label="Employer (optional)" {...register("employerName")} />
              <Controller
                control={control}
                name="monthlyIncome"
                render={({ field }) => (
                  <NumberInput
                    label="Monthly income (optional)"
                    min={0}
                    value={field.value}
                    onChange={(value) =>
                      field.onChange(typeof value === "number" ? value : undefined)
                    }
                  />
                )}
              />
              <Checkbox
                label="I consent to a credit and background screening as part of this application."
                error={errors.consentToScreening?.message}
                {...register("consentToScreening")}
              />
              {submitMutation.isError && (
                <Alert color="red" title="Could not submit" styles={dangerAlertStyles}>
                  {submitMutation.error instanceof TrellisApiError
                    ? submitMutation.error.message
                    : "Something went wrong. Please try again."}
                </Alert>
              )}
              <Button type="submit" loading={submitMutation.isPending}>
                Submit application
              </Button>
            </Stack>
          </form>
        )}
      </Stack>
    </Container>
  );
}
