-- Postgres RLS (data_model.md, multi_tenant_design.md) only applies to a role that is neither a
-- superuser nor the owner of the table it's querying — FORCE ROW LEVEL SECURITY only extends RLS
-- to the table's own owner, and does nothing for a superuser, which always bypasses RLS outright.
-- POSTGRES_USER in docker-compose.yml is a superuser by default. Every migration
-- (dotnet ef database update) runs as that superuser/owner role; the running API must connect as
-- this separate, unprivileged role instead, or layer 1 of the 3-layer isolation model is inert.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trellis_app') THEN
        CREATE ROLE trellis_app LOGIN PASSWORD 'trellis_app_dev_only' NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;

GRANT CONNECT ON DATABASE trellis_platform TO trellis_app;
