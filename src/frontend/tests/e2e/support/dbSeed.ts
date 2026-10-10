import { createHash } from "node:crypto";
import { Client } from "pg";

// Same dev-only local Postgres credentials documented in src/backend/Trellis.Api/.env.example
// and src/infrastructure/docker-compose.yml — duplicated here rather than read from the
// backend's .env, since project_architecture.md's CI lint rule forbids cross-imports between
// src/backend and src/frontend. Connects as the privileged `trellis` role (not `trellis_app`):
// RLS doesn't apply to the table owner, so this seeds/cleans up test data the same way the
// backend's own migrations do, independent of the app-layer session context this test has none
// of yet (it hasn't logged in).
const CONNECTION_STRING = "postgres://trellis:trellis_dev_only@localhost:5432/trellis_platform";

// A fixed, precomputed Argon2id hash (security_guide.md's parameters) for the plaintext below —
// generated once via Argon2PasswordHasher, hardcoded here since re-hashing at test time would
// need the backend's own hasher, not a reimplementation in TypeScript that could drift from it.
const SEED_PASSWORD_HASH = "nUh9NgQbo28zQoE6Nus8Ug==:drONev4S6qxCnjtKRsQ5tfpzzUUm0Qloxk0rYL0mVxI=";
export const SEED_PASSWORD = "E2eTestPassw0rd!";

/** Public URLs identify a tenant by slug, never its GUID. Seeded tenants get a unique, valid slug derived from their fixed id, so specs can build public URLs without a lookup. */
export function tenantSlugFor(tenantId: string): string {
  return `t-${tenantId.replace(/-/g, "").slice(0, 10)}`;
}

export async function seedSuperAdmin(username: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_auth.app_user (user_id, tenant_id, username, password_hash, role, status)
       values (gen_random_uuid(), null, $1, $2, 'SUPER_ADMIN', 'ACTIVE')`,
      [username, SEED_PASSWORD_HASH],
    );
  } finally {
    await client.end();
  }
}

/** Seeded directly (not through the Super Admin provisioning flow) so its id is known ahead of time — needed wherever a test asserts a server-resolved tenant name, e.g. vendor.spec.ts's cross-tenant view (ITenantService.GetByIdAsync). Defaults to STARTER; ai.spec.ts passes PROFESSIONAL to exercise the Ai tier gate. */
export async function seedTenant(
  tenantId: string,
  tenantName: string,
  tier: string = "STARTER",
  slug: string = tenantSlugFor(tenantId),
  countryCode: string = "US",
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_tenant.tenant (tenant_id, tenant_name, slug, country_code, status, subscription_tier, created_at, updated_at)
       values ($1, $2, $3, $5, 'ACTIVE', $4, now(), now())`,
      [tenantId, tenantName, slug, tier, countryCode],
    );
  } finally {
    await client.end();
  }
}

export async function seedTenantAdmin(username: string, tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_auth.app_user (user_id, tenant_id, username, password_hash, role, status)
       values (gen_random_uuid(), $1, $2, $3, 'TENANT_ADMIN', 'ACTIVE')`,
      [tenantId, username, SEED_PASSWORD_HASH],
    );
  } finally {
    await client.end();
  }
}

/** workflow.spec.ts's approving user — TenantAdminOrDepartmentLead's DEPARTMENT_STAFF branch needs department_ids set at seed time (the JWT bakes them in at login, department_id claims aren't editable after the fact). */
export async function seedDepartmentStaff(
  username: string,
  tenantId: string,
  departmentIds: string[],
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_auth.app_user (user_id, tenant_id, username, password_hash, role, department_ids, status)
       values (gen_random_uuid(), $1, $2, $3, 'DEPARTMENT_STAFF', $4, 'ACTIVE')`,
      [tenantId, username, SEED_PASSWORD_HASH, departmentIds],
    );
  } finally {
    await client.end();
  }
}

/** Seeded directly (not through the UI) so its id is known ahead of time, for seedDepartmentStaff's department_ids to reference. */
export async function seedDepartment(
  departmentId: string,
  tenantId: string,
  name: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_workflow.department (department_id, tenant_id, name, status, created_at, updated_at)
       values ($1, $2, $3, 'ACTIVE', now(), now())`,
      [departmentId, tenantId, name],
    );
  } finally {
    await client.end();
  }
}

export async function deleteWorkflowDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_workflow.workflow_step_instance where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_workflow.workflow_instance where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_workflow.workflow_routing_rule where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_workflow.workflow_step_template where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_workflow.workflow_template where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_workflow.department where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

export async function deleteDocumentDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_document.document_version where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_document.document where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

/** propertyasset.spec.ts's own cleanup — Owner/Property/UnitAsset are archived (never hard-deleted) by the app, so tests still delete their rows directly to keep the DB clean between runs. */
export async function deletePropertyAssetDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_property.lease where tenant_id = $1`, [tenantId]);
    await client.query(`delete from trellis_property.unit_asset where tenant_id = $1`, [tenantId]);
    await client.query(`delete from trellis_property.property where tenant_id = $1`, [tenantId]);
    await client.query(`delete from trellis_property.owner where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

/** Seeded directly (not through the UI) so its id is known ahead of time, for vendor-assignment seeding to reference — mirrors seedDepartment's own reasoning. workflow_template_id and created_by are arbitrary uuids; nothing validates either FK-lessly. */
export async function seedWorkflowInstance(
  workflowInstanceId: string,
  tenantId: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_workflow.workflow_instance
         (workflow_instance_id, tenant_id, workflow_template_id, cost_amount, status, current_step_order, created_by, created_at, updated_at)
       values ($1, $2, gen_random_uuid(), 500, 'IN_PROGRESS', 1, gen_random_uuid(), now(), now())`,
      [workflowInstanceId, tenantId],
    );
  } finally {
    await client.end();
  }
}

/** vendor.spec.ts's cross-tenant vendor — global (no tenant_id), created once and engaged by two separate tenants. */
export async function seedVendor(vendorId: string, vendorName: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_vendor.vendor (vendor_id, vendor_name, category, created_at)
       values ($1, $2, 'MAINTENANCE', now())`,
      [vendorId, vendorName],
    );
  } finally {
    await client.end();
  }
}

export async function seedTenantVendorRelationship(
  tenantId: string,
  vendorId: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_vendor.tenant_vendor_relationship
         (tenant_vendor_relationship_id, tenant_id, vendor_id, status, created_at)
       values (gen_random_uuid(), $1, $2, 'ACTIVE', now())`,
      [tenantId, vendorId],
    );
  } finally {
    await client.end();
  }
}

/** The vendor's one login — no tenant_id (BDR-VEN-004), a first-class app_user row per security_guide.md. */
export async function seedVendorUser(username: string, vendorId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_auth.app_user (user_id, tenant_id, vendor_id, username, password_hash, role, status)
       values (gen_random_uuid(), null, $1, $2, $3, 'VENDOR_USER', 'ACTIVE')`,
      [vendorId, username, SEED_PASSWORD_HASH],
    );
  } finally {
    await client.end();
  }
}

export async function seedVendorAssignment(
  vendorId: string,
  tenantId: string,
  workItemId: string,
  workItemType: string = "WORKFLOW_INSTANCE",
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_vendor.vendor_assignment
         (assignment_id, vendor_id, tenant_id, work_item_type, work_item_id, status, assigned_at)
       values (gen_random_uuid(), $1, $2, $3, $4, 'ASSIGNED', now())`,
      [vendorId, tenantId, workItemType, workItemId],
    );
  } finally {
    await client.end();
  }
}

/** inspection-offline.spec.ts's own seeded inspection — a fixed baseline of 5 checklist items, mirroring InspectionChecklistDefaults.BaselineLabels exactly so the offline capture UI has real items to edit. */
export async function seedInspection(
  inspectionId: string,
  tenantId: string,
  propertyId: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_inspection.inspection
         (inspection_id, tenant_id, property_id, scheduled_by_user_id, status, scheduled_at, created_at, updated_at)
       values ($1, $2, $3, gen_random_uuid(), 'SCHEDULED', now(), now(), now())`,
      [inspectionId, tenantId, propertyId],
    );

    const labels = ["Smoke Detectors", "Plumbing", "HVAC", "Structural", "Electrical"];
    for (const [index, label] of labels.entries()) {
      await client.query(
        `insert into trellis_inspection.inspection_item
           (inspection_item_id, tenant_id, inspection_id, label, status, sort_order, created_at, updated_at)
         values (gen_random_uuid(), $1, $2, $3, 'PENDING', $4, now(), now())`,
        [tenantId, inspectionId, label, index],
      );
    }
  } finally {
    await client.end();
  }
}

export async function deleteInspectionDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_inspection.inspection_photo where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_inspection.inspection_item where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_inspection.inspection where tenant_id = $1`, [
      tenantId,
    ]);
  } finally {
    await client.end();
  }
}

export async function deleteVendorDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_vendor.vendor_assignment where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(
      `delete from trellis_vendor.tenant_vendor_relationship where tenant_id = $1`,
      [tenantId],
    );
  } finally {
    await client.end();
  }
}

export async function deleteVendor(vendorId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_vendor.vendor where vendor_id = $1`, [vendorId]);
  } finally {
    await client.end();
  }
}

export async function deleteAppUsers(usernames: string[]): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_auth.app_user where username = any($1)`, [usernames]);
  } finally {
    await client.end();
  }
}

export async function deleteTenantByName(tenantName: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `delete from trellis_tenant.tenant_domain where tenant_id in (select tenant_id from trellis_tenant.tenant where tenant_name = $1)`,
      [tenantName],
    );
    await client.query(`delete from trellis_tenant.tenant where tenant_name = $1`, [tenantName]);
  } finally {
    await client.end();
  }
}

export async function deleteAiDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_ai.override_log where tenant_id = $1`, [tenantId]);
    await client.query(`delete from trellis_ai.routing_suggestion where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_ai.extraction_job where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

/** ai.spec.ts's own read-back — there's no override-log UI surface (BDR-AI-004's structured-query table is for future retraining, not a screen), so the e2e proof queries the row directly instead. */
export async function countOverrideLogEntriesForTenant(
  tenantId: string,
  suggestionType: string,
): Promise<number> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select count(*)::int as count from trellis_ai.override_log where tenant_id = $1 and suggestion_type = $2`,
      [tenantId, suggestionType],
    );
    return result.rows[0].count;
  } finally {
    await client.end();
  }
}

/** billing.spec.ts's own fixture — a full billing cycle can't be driven through the UI alone (the daily UsageSnapshotBackgroundService poll isn't something a test can trigger on demand), so the invoice/subscription state it asserts against is seeded directly, mirroring seedWorkflowInstance's own precedent. */
export async function seedSubscription(
  subscriptionId: string,
  tenantId: string,
  status: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_billing.subscription (subscription_id, tenant_id, status, currency, billing_cycle_anchor_day, created_at)
       values ($1, $2, $3, 'USD', 1, now())`,
      [subscriptionId, tenantId, status],
    );
  } finally {
    await client.end();
  }
}

export async function seedInvoiceWithLineItem(
  invoiceId: string,
  tenantId: string,
  subscriptionId: string,
  totalAmount: number,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_billing.invoice
         (invoice_id, tenant_id, subscription_id, billing_period_start, billing_period_end, currency,
          subtotal_amount, tax_amount, total_amount, status, due_at, paid_at, created_at)
       values ($1, $2, $3, current_date, current_date + interval '30 days', 'USD', $4, 0, $4, 'PAID', now(), now(), now())`,
      [invoiceId, tenantId, subscriptionId, totalAmount],
    );
    await client.query(
      `insert into trellis_billing.invoice_line_item (line_item_id, tenant_id, invoice_id, description, quantity, unit_amount, amount)
       values (gen_random_uuid(), $1, $2, $3, 1, $4, $4)`,
      [tenantId, invoiceId, `${totalAmount} active units × 1.00`, totalAmount],
    );
  } finally {
    await client.end();
  }
}

export async function seedModuleEntitlement(
  tenantId: string,
  moduleKey: string,
  status: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_billing.module_entitlement (entitlement_id, tenant_id, module_key, status, monthly_price, enabled_at)
       values (gen_random_uuid(), $1, $2, $3, 0, now())
       on conflict (tenant_id, module_key) do update set status = excluded.status`,
      [tenantId, moduleKey, status],
    );
  } finally {
    await client.end();
  }
}

/** reporting.spec.ts's own Owner — real cross-module data an Owner report and the dashboard's document-compliance count both read from. */
export async function seedOwner(
  ownerId: string,
  tenantId: string,
  name: string,
  email: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_property.owner (owner_id, tenant_id, name, email, status, created_at, updated_at)
       values ($1, $2, $3, $4, 'ACTIVE', now(), now())`,
      [ownerId, tenantId, name, email],
    );
  } finally {
    await client.end();
  }
}

/** reporting.spec.ts's own Property — PropertyService.ListPropertyIdsByOwnerAsync's real data source for an Owner report's maintenance-cost aggregate. */
export async function seedProperty(
  propertyId: string,
  tenantId: string,
  ownerId: string,
  address: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_property.property (property_id, tenant_id, owner_id, address, property_type, status, created_at, updated_at)
       values ($1, $2, $3, $4, 'RESIDENTIAL', 'ACTIVE', now(), now())`,
      [propertyId, tenantId, ownerId, address],
    );
  } finally {
    await client.end();
  }
}

/** owner_report/report_share_link only — never trellis_audit.audit_record, which is append-only and can't be deleted (its own trigger rejects DELETE unconditionally, even for this privileged role). */
export async function deleteReportingDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_reporting.report_share_link where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_reporting.owner_report where tenant_id = $1`, [
      tenantId,
    ]);
  } finally {
    await client.end();
  }
}

export async function deleteBillingDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_billing.payment_transaction where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_billing.invoice_line_item where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_billing.invoice where tenant_id = $1`, [tenantId]);
    await client.query(`delete from trellis_billing.module_entitlement where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_billing.payment_method where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_billing.subscription where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

/** leasing.spec.ts's own vacant unit — listing creation only offers active, vacant units, and Unit creation itself is already covered by propertyasset.spec.ts. */
export async function seedUnitAsset(
  unitAssetId: string,
  tenantId: string,
  propertyId: string,
  label: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_property.unit_asset (unit_asset_id, tenant_id, property_id, label, status, occupancy_status, created_at, updated_at)
       values ($1, $2, $3, $4, 'ACTIVE', 'VACANT', now(), now())`,
      [unitAssetId, tenantId, propertyId, label],
    );
  } finally {
    await client.end();
  }
}

export interface LeaseRow {
  source: string;
  status: string;
  termType: string;
  endDate: string | null;
  endReason: string | null;
  endedBy: string | null;
  endNote: string | null;
}

/** BDR-LSE-009/011 — a unit's lease records, oldest first, with dates as plain yyyy-mm-dd strings. */
export async function getLeasesForUnit(unitAssetId: string): Promise<LeaseRow[]> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select source, status, term_type, to_char(end_date, 'YYYY-MM-DD') as end_date, end_reason, ended_by, end_note
       from trellis_property.lease where unit_asset_id = $1 order by created_at`,
      [unitAssetId],
    );
    return result.rows.map((r) => ({
      source: r.source as string,
      status: r.status as string,
      termType: r.term_type as string,
      endDate: r.end_date as string | null,
      endReason: r.end_reason as string | null,
      endedBy: r.ended_by as string | null,
      endNote: r.end_note as string | null,
    }));
  } finally {
    await client.end();
  }
}

/** BDR-LSE-010 — a lease's end date as yyyy-mm-dd, looked up by unit (the unit's most recent lease). */
export async function getLatestLeaseEndDate(unitAssetId: string): Promise<string | null> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select to_char(end_date, 'YYYY-MM-DD') as end_date from trellis_property.lease
       where unit_asset_id = $1 order by created_at desc limit 1`,
      [unitAssetId],
    );
    return result.rows[0]?.end_date ?? null;
  } finally {
    await client.end();
  }
}

/** BDR-AST-005 — yyyy-mm-dd, or null while occupied or when the vacancy start is unknown. */
export async function getUnitVacantSince(unitAssetId: string): Promise<string | null> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select to_char(vacant_since, 'YYYY-MM-DD') as vacant_since from trellis_property.unit_asset where unit_asset_id = $1`,
      [unitAssetId],
    );
    return result.rows[0].vacant_since as string | null;
  } finally {
    await client.end();
  }
}

export async function getUnitOccupancyStatus(unitAssetId: string): Promise<string> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select occupancy_status from trellis_property.unit_asset where unit_asset_id = $1`,
      [unitAssetId],
    );
    return result.rows[0].occupancy_status as string;
  } finally {
    await client.end();
  }
}

/** The applicant's emailed link can't be read by a test, so a token with a known raw value is seeded directly (only its SHA-256 hash is ever stored, exactly as the app does). The listing is looked up by unit, since it was created through the UI. */
export async function seedApplicationLinkToken(
  tenantId: string,
  unitAssetId: string,
  applicantEmail: string,
  rawToken: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const hash = createHash("sha256").update(rawToken).digest("hex");
    await client.query(
      `insert into trellis_leasing.application_link_token
         (application_link_token_id, tenant_id, vacancy_listing_id, applicant_email, token_hash, status, expires_at, created_at)
       select gen_random_uuid(), $1, vacancy_listing_id, $3, $4, 'ACTIVE', now() + interval '7 days', now()
       from trellis_leasing.vacancy_listing where tenant_id = $1 and unit_asset_id = $2 and status in ('LISTED','UNDER_APPLICATION')`,
      [tenantId, unitAssetId, applicantEmail, hash],
    );
  } finally {
    await client.end();
  }
}

/** The unsigned lease draft a staff member would upload through the normal Documents flow before sending for signature. */
export async function seedLeaseDraftDocument(
  documentId: string,
  tenantId: string,
  unitAssetId: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const versionId = documentId.replace(/^.{8}/, "deadbeef");
    await client.query(
      `insert into trellis_document.document (document_id, tenant_id, unit_asset_id, document_type, status, created_at, updated_at)
       values ($1, $2, $3, 'LEASE', 'ACTIVE', now(), now())`,
      [documentId, tenantId, unitAssetId],
    );
    await client.query(
      `insert into trellis_document.document_version
         (document_version_id, document_id, tenant_id, version_number, file_name, content_type, file_size_bytes, storage_key, scan_status, uploaded_by, created_at)
       values ($1, $2, $3, 1, 'lease-draft.pdf', 'application/pdf', 4, $4, 'CLEAN', gen_random_uuid(), now())`,
      [versionId, documentId, tenantId, `e2e/${documentId}`],
    );
    await client.query(
      `update trellis_document.document set current_version_id = $2 where document_id = $1`,
      [documentId, versionId],
    );
  } finally {
    await client.end();
  }
}

export async function getEnvelopeIdForTenant(tenantId: string): Promise<string> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select envelope_id from trellis_leasing.application where tenant_id = $1 and envelope_id is not null`,
      [tenantId],
    );
    return result.rows[0].envelope_id as string;
  } finally {
    await client.end();
  }
}

/** Never trellis_audit.audit_record (append-only, its own trigger rejects DELETE). application_event is deletable here only because this is the privileged owner role — trellis_app has SELECT/INSERT only. */
export async function deleteLeasingDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `delete from trellis_leasing.leasing_webhook_event
       where payload_json like any (
         select '%' || envelope_id || '%' from trellis_leasing.application
         where tenant_id = $1 and envelope_id is not null)`,
      [tenantId],
    );
    for (const table of [
      "renewal_offer",
      "screening_result",
      "application_event",
      "application_link_token",
      "application",
      "vacancy_listing_photo",
      "vacancy_listing",
    ]) {
      await client.query(`delete from trellis_leasing.${table} where tenant_id = $1`, [tenantId]);
    }
  } finally {
    await client.end();
  }
}

/** rentledger.spec.ts / leasing.spec.ts — a schedule seeded directly, e.g. the prior tenancy a new lease must supersede. */
export async function seedRentSchedule(
  scheduleId: string,
  tenantId: string,
  unitAssetId: string,
  rentAmount: number,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    // Every schedule tracks a lease (BDR-LSE-009). A seeded schedule stands for a tenancy started
    // by hand, so it gets a MANUAL lease of its own — reusing the schedule's id, the same way the
    // migration that introduced lease records backfilled existing schedules.
    await client.query(
      `insert into trellis_property.lease
         (lease_id, tenant_id, unit_asset_id, source, start_date, term_type, status, created_at, updated_at)
       values ($1, $2, $3, 'MANUAL', '2025-01-01', 'NOT_RECORDED', 'ACTIVE', now(), now())`,
      [scheduleId, tenantId, unitAssetId],
    );
    await client.query(
      `insert into trellis_rentledger.rent_schedule
         (rent_schedule_id, tenant_id, unit_asset_id, lease_id, rent_amount, currency, start_date, due_day_of_month, grace_period_days, status, created_at, updated_at)
       values ($1, $2, $3, $1, $4, 'USD', '2025-01-01', 1, 5, 'ACTIVE', now(), now())`,
      [scheduleId, tenantId, unitAssetId, rentAmount],
    );
  } finally {
    await client.end();
  }
}

export interface RentScheduleRow {
  status: string;
  rentAmount: number;
  dueDayOfMonth: number;
  hasLeaseApplication: boolean;
}

export async function getRentSchedulesForUnit(unitAssetId: string): Promise<RentScheduleRow[]> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select status, rent_amount, due_day_of_month, lease_application_id is not null as has_lease
       from trellis_rentledger.rent_schedule where unit_asset_id = $1 order by created_at`,
      [unitAssetId],
    );
    return result.rows.map((r) => ({
      status: r.status as string,
      rentAmount: Number(r.rent_amount),
      dueDayOfMonth: r.due_day_of_month as number,
      hasLeaseApplication: r.has_lease as boolean,
    }));
  } finally {
    await client.end();
  }
}

/** Payments first, then schedules. Never trellis_audit.audit_record (append-only). */
export async function deleteRentLedgerDataForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_rentledger.rent_payment_record where tenant_id = $1`, [
      tenantId,
    ]);
    await client.query(`delete from trellis_rentledger.rent_schedule where tenant_id = $1`, [
      tenantId,
    ]);
  } finally {
    await client.end();
  }
}

export interface UnitDocumentRow {
  documentType: string;
  versionCount: number;
  currentVersionNumber: number;
}

/** The documents on a unit with how many versions each has and which one is current — BDR-LSE-006's "versioned Document". */
export async function getDocumentsForUnit(unitAssetId: string): Promise<UnitDocumentRow[]> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    const result = await client.query(
      `select d.document_type, count(v.*)::int as version_count,
              (select v2.version_number from trellis_document.document_version v2 where v2.document_version_id = d.current_version_id) as current_version
       from trellis_document.document d
       left join trellis_document.document_version v on v.document_id = d.document_id
       where d.unit_asset_id = $1
       group by d.document_id, d.document_type, d.current_version_id
       order by d.created_at`,
      [unitAssetId],
    );
    return result.rows.map((r) => ({
      documentType: r.document_type as string,
      versionCount: r.version_count as number,
      currentVersionNumber: r.current_version as number,
    }));
  } finally {
    await client.end();
  }
}

/** A custom domain already proven by DNS — real DNS verification can't be driven from a test, so the VERIFIED row is seeded directly (the verification logic itself is unit- and smoke-tested). */
export async function seedVerifiedDomain(tenantId: string, domain: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_tenant.tenant_domain (tenant_domain_id, tenant_id, domain, status, verification_token, verified_at, created_at)
       values (gen_random_uuid(), $1, $2, 'VERIFIED', 'e2e-token', now(), now())`,
      [tenantId, domain],
    );
  } finally {
    await client.end();
  }
}

export async function deleteTenantDomainsForTenant(tenantId: string): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(`delete from trellis_tenant.tenant_domain where tenant_id = $1`, [tenantId]);
  } finally {
    await client.end();
  }
}

/** A LISTED vacancy listing seeded directly, so public pages have something to show without driving the staff listing flow again. */
export async function seedVacancyListing(
  listingId: string,
  tenantId: string,
  unitAssetId: string,
  description: string,
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_leasing.vacancy_listing
         (vacancy_listing_id, tenant_id, unit_asset_id, rent_amount, currency, available_from, description, status, created_by, created_at, updated_at)
       values ($1, $2, $3, 1200, 'USD', '2026-12-01', $4, 'LISTED', gen_random_uuid(), now(), now())`,
      [listingId, tenantId, unitAssetId, description],
    );
  } finally {
    await client.end();
  }
}
