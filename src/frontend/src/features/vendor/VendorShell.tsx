import { Button, Group, Stack, Text } from "@mantine/core";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { authApi } from "../../api/auth";
import { useAuth } from "../../auth/AuthContext";

/** BDR-VEN-004 — one login, cross-tenant: no tenant switcher here, since a Vendor's own JWT carries vendor_id, never tenant_id. */
export function VendorShell() {
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
            to="/vendor"
            end
            style={({ isActive }) => ({ fontWeight: isActive ? 700 : 400 })}
          >
            Assignments
          </NavLink>
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
