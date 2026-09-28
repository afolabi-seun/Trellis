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
): Promise<void> {
  const client = new Client({ connectionString: CONNECTION_STRING });
  await client.connect();
  try {
    await client.query(
      `insert into trellis_tenant.tenant (tenant_id, tenant_name, country_code, status, subscription_tier, created_at, updated_at)
       values ($1, $2, 'US', 'ACTIVE', $3, now(), now())`,
      [tenantId, tenantName, tier],
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
       values ($1, $2, $3, $4, 'SingleFamily', 'ACTIVE', now(), now())`,
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
