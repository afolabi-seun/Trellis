import { Navigate, Route, Routes } from "react-router-dom";

// frontend_architecture.md, "One SPA, Role-Based Routing" — placeholder route trees for the
// three personas. Real screens land module-by-module per roadmap.md; this is the shell only.
// UI-layer routing is UX, not the security boundary — the API enforces access regardless of
// what this router renders (see the same doc's caveat).

function Placeholder({ label }: { label: string }) {
  return <div>{label}</div>;
}

export function App() {
  return (
    <Routes>
      <Route path="/admin/*" element={<Placeholder label="Tenant Admin" />} />
      <Route path="/vendor/*" element={<Placeholder label="Vendor Portal" />} />
      <Route path="/platform/*" element={<Placeholder label="Platform Operator" />} />
      <Route path="/" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
