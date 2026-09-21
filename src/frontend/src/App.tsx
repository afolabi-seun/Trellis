import { Navigate, Route, Routes } from "react-router-dom";
import { RequireRole } from "./auth/RequireRole";
import { ForgotPasswordPage } from "./features/auth/ForgotPasswordPage";
import { LoginPage } from "./features/auth/LoginPage";
import { MfaVerifyPage } from "./features/auth/MfaVerifyPage";
import { ResetPasswordPage } from "./features/auth/ResetPasswordPage";
import { TenantsListPage } from "./features/platform/TenantsListPage";

// frontend_architecture.md, "One SPA, Role-Based Routing" — placeholder route trees for the
// two personas without a real screen yet. Real screens land module-by-module per roadmap.md;
// UI-layer routing is UX, not the security boundary — the API enforces access regardless of
// what this router renders (see the same doc's caveat).

function Placeholder({ label }: { label: string }) {
  return <div>{label}</div>;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/mfa-verify" element={<MfaVerifyPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      <Route element={<RequireRole allowedRoles={["TENANT_ADMIN", "DEPARTMENT_STAFF"]} />}>
        <Route path="/admin/*" element={<Placeholder label="Tenant Admin" />} />
      </Route>

      <Route element={<RequireRole allowedRoles={["VENDOR_USER"]} />}>
        <Route path="/vendor/*" element={<Placeholder label="Vendor Portal" />} />
      </Route>

      <Route element={<RequireRole allowedRoles={["SUPER_ADMIN"]} />}>
        <Route path="/platform" element={<TenantsListPage />} />
      </Route>

      <Route path="/" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
