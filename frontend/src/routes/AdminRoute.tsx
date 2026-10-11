import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { Spinner } from "../components/ui";
import { AdminForbiddenPage } from "../pages/admin/AdminForbiddenPage";
import { adminEntryRedirect, isAdminRole } from "../lib/adminPaths";

function Inspecting() {
  return (
    <main className="loading">
      <Spinner /> Memeriksa sesi…
    </main>
  );
}

export function AdminRoute() {
  const { session, loading } = useAuth();
  const location = useLocation();
  if (loading) return <Inspecting />;
  if (!session || !isAdminRole(session.user.role))
    return (
      <Navigate
        to={adminEntryRedirect(location.pathname, location.search)}
        replace
      />
    );
  return <Outlet />;
}

export function PlatformAdminRoute() {
  const { session, loading } = useAuth();
  const location = useLocation();
  if (loading) return <Inspecting />;
  if (!session || !isAdminRole(session.user.role))
    return (
      <Navigate
        to={adminEntryRedirect(location.pathname, location.search)}
        replace
      />
    );
  if (session.user.role !== "PlatformAdmin") return <AdminForbiddenPage />;
  return <Outlet />;
}