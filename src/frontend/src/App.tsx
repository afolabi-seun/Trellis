import { Navigate, Route, Routes } from "react-router-dom";
import { RequireRole } from "./auth/RequireRole";
import { AdminShell } from "./features/admin/AdminShell";
import { ExtractionJobDetailPage } from "./features/admin/ai/ExtractionJobDetailPage";
import { ExtractionReviewQueuePage } from "./features/admin/ai/ExtractionReviewQueuePage";
import { BillingSettingsPage } from "./features/admin/billing/BillingSettingsPage";
import { DocumentsLibraryPage } from "./features/admin/document/DocumentsLibraryPage";
import { InspectionDetailPage } from "./features/admin/inspection/InspectionDetailPage";
import { InspectionsListPage } from "./features/admin/inspection/InspectionsListPage";
import { ApplicationsReviewPage } from "./features/admin/leasing/ApplicationsReviewPage";
import { VacancyListingDetailPage } from "./features/admin/leasing/VacancyListingDetailPage";
import { VacancyListingsPage } from "./features/admin/leasing/VacancyListingsPage";
import { OwnersListPage } from "./features/admin/propertyasset/OwnersListPage";
import { PropertiesListPage } from "./features/admin/propertyasset/PropertiesListPage";
import { PropertyDetailPage } from "./features/admin/propertyasset/PropertyDetailPage";
import { OwnerReportsPage } from "./features/admin/reporting/OwnerReportsPage";
import { ReportingDashboardPage } from "./features/admin/reporting/ReportingDashboardPage";
import { VendorsListPage } from "./features/admin/vendor/VendorsListPage";
import { DepartmentsListPage } from "./features/admin/workflow/DepartmentsListPage";
import { WorkflowInstancesBoardPage } from "./features/admin/workflow/WorkflowInstancesBoardPage";
import { WorkflowTemplatesListPage } from "./features/admin/workflow/WorkflowTemplatesListPage";
import { ForgotPasswordPage } from "./features/auth/ForgotPasswordPage";
import { LoginPage } from "./features/auth/LoginPage";
import { MfaVerifyPage } from "./features/auth/MfaVerifyPage";
import { ResetPasswordPage } from "./features/auth/ResetPasswordPage";
import { ApplyPage } from "./features/public/leasing/ApplyPage";
import { PublicVacancyListingDetailPage } from "./features/public/leasing/PublicVacancyListingDetailPage";
import { PublicVacancyListingsPage } from "./features/public/leasing/PublicVacancyListingsPage";
import { PlatformBillingPage } from "./features/platform/PlatformBillingPage";
import { TenantsListPage } from "./features/platform/TenantsListPage";
import { VendorAssignmentDetailPage } from "./features/vendor/VendorAssignmentDetailPage";
import { VendorAssignmentsListPage } from "./features/vendor/VendorAssignmentsListPage";
import { VendorInspectionCapturePage } from "./features/vendor/VendorInspectionCapturePage";
import { VendorShell } from "./features/vendor/VendorShell";

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/mfa-verify" element={<MfaVerifyPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      <Route path="/vacancies/:tenantId" element={<PublicVacancyListingsPage />} />
      <Route path="/vacancies/:tenantId/:listingId" element={<PublicVacancyListingDetailPage />} />
      <Route path="/apply/:token" element={<ApplyPage />} />

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
          <Route path="vendors" element={<VendorsListPage />} />
          <Route path="inspections" element={<InspectionsListPage />} />
          <Route path="inspections/:id" element={<InspectionDetailPage />} />
          <Route path="extraction-jobs" element={<ExtractionReviewQueuePage />} />
          <Route path="extraction-jobs/:id" element={<ExtractionJobDetailPage />} />
          <Route path="vacancy-listings" element={<VacancyListingsPage />} />
          <Route path="vacancy-listings/:id" element={<VacancyListingDetailPage />} />
          <Route path="applications" element={<ApplicationsReviewPage />} />
          <Route path="reporting" element={<ReportingDashboardPage />} />
          <Route element={<RequireRole allowedRoles={["TENANT_ADMIN"]} />}>
            <Route path="billing" element={<BillingSettingsPage />} />
            <Route path="owner-reports" element={<OwnerReportsPage />} />
          </Route>
        </Route>
      </Route>

      <Route element={<RequireRole allowedRoles={["VENDOR_USER"]} />}>
        <Route path="/vendor" element={<VendorShell />}>
          <Route index element={<VendorAssignmentsListPage />} />
          <Route path="assignments/:workItemId" element={<VendorAssignmentDetailPage />} />
          <Route path="inspections/:workItemId" element={<VendorInspectionCapturePage />} />
        </Route>
      </Route>

      <Route element={<RequireRole allowedRoles={["SUPER_ADMIN"]} />}>
        <Route path="/platform" element={<Navigate to="/platform/tenants" replace />} />
        <Route path="/platform/tenants" element={<TenantsListPage />} />
        <Route path="/platform/billing" element={<PlatformBillingPage />} />
      </Route>

      <Route path="/" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
