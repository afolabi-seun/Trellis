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
