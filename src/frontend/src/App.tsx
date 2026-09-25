import { Navigate, Route, Routes } from "react-router-dom";
import { RequireRole } from "./auth/RequireRole";
import { AdminShell } from "./features/admin/AdminShell";
import { DocumentsLibraryPage } from "./features/admin/document/DocumentsLibraryPage";
import { OwnersListPage } from "./features/admin/propertyasset/OwnersListPage";
import { PropertiesListPage } from "./features/admin/propertyasset/PropertiesListPage";
import { PropertyDetailPage } from "./features/admin/propertyasset/PropertyDetailPage";
import { DepartmentsListPage } from "./features/admin/workflow/DepartmentsListPage";
import { WorkflowInstancesBoardPage } from "./features/admin/workflow/WorkflowInstancesBoardPage";
import { WorkflowTemplatesListPage } from "./features/admin/workflow/WorkflowTemplatesListPage";
import { ForgotPasswordPage } from "./features/auth/ForgotPasswordPage";
import { LoginPage } from "./features/auth/LoginPage";
import { MfaVerifyPage } from "./features/auth/MfaVerifyPage";
import { ResetPasswordPage } from "./features/auth/ResetPasswordPage";
import { TenantsListPage } from "./features/platform/TenantsListPage";

// frontend_architecture.md, "One SPA, Role-Based Routing" — the Vendor tree has no real screen
// yet (a later phase). Real screens land module-by-module per roadmap.md; UI-layer routing is
// UX, not the security boundary — the API enforces access regardless of what this router renders
// (see the same doc's caveat).

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
        <Route path="/admin" element={<AdminShell />}>
          <Route index element={<Navigate to="properties" replace />} />
          <Route path="properties" element={<PropertiesListPage />} />
          <Route path="properties/:propertyId" element={<PropertyDetailPage />} />
          <Route path="owners" element={<OwnersListPage />} />
          <Route path="departments" element={<DepartmentsListPage />} />
          <Route path="workflow-templates" element={<WorkflowTemplatesListPage />} />
          <Route path="workflow-instances" element={<WorkflowInstancesBoardPage />} />
          <Route path="documents" element={<DocumentsLibraryPage />} />
        </Route>
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
