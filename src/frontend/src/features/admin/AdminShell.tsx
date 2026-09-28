import { Button, Group, Stack, Text } from "@mantine/core";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { authApi } from "../../api/auth";
import { useAuth } from "../../auth/AuthContext";

/** Minimal Tenant Admin shell — nav between PropertyAsset's screens, matching frontend_architecture.md's Repository & Deployment tree (one persona-module subfolder per reachable backend module). */
export function AdminShell() {
  const navigate = useNavigate();
  const { claims, logout } = useAuth();

  const handleLogout = async () => {
    try {
      await authApi.logout();
    } finally {
      logout();
      navigate("/login", { replace: true });
    }
  };

  return (
    <Stack gap={0}>
      <Group
        justify="space-between"
        p="md"
        style={{ borderBottom: "1px solid var(--mantine-color-gray-3)" }}
      >
        <Group gap="lg">
          <NavLink
            to="/admin/properties"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Properties
          </NavLink>
          <NavLink
            to="/admin/owners"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Owners
          </NavLink>
          <NavLink
            to="/admin/departments"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Departments
          </NavLink>
          <NavLink
            to="/admin/workflow-templates"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Workflow templates
          </NavLink>
          <NavLink
            to="/admin/workflow-instances"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Workflow instances
          </NavLink>
          <NavLink
            to="/admin/documents"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Documents
          </NavLink>
          <NavLink
            to="/admin/vendors"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Vendors
          </NavLink>
          <NavLink
            to="/admin/inspections"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Inspections
          </NavLink>
          <NavLink
            to="/admin/extraction-jobs"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Extraction review
          </NavLink>
          <NavLink
            to="/admin/reporting"
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Reporting
          </NavLink>
          {claims?.role === "TENANT_ADMIN" && (
            <NavLink
              to="/admin/billing"
              style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
            >
              Billing
            </NavLink>
          )}
          {claims?.role === "TENANT_ADMIN" && (
            <NavLink
              to="/admin/owner-reports"
              style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
            >
              Owner reports
            </NavLink>
          )}
        </Group>
        <Group>
          <Text c="gray.7" size="sm">
            {claims?.email}
          </Text>
          <Button variant="subtle" onClick={handleLogout}>
            Sign out
          </Button>
        </Group>
      </Group>
      <Outlet />
    </Stack>
  );
}
