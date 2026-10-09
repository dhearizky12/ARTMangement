import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { Spinner } from "../components/ui";
import type { Role } from "../api/authApi";
export function ProtectedRoute({
  platform = false,
  role,
}: {
  platform?: boolean;
  role?: Role;
}) {
  const { session, loading } = useAuth();
  const location = useLocation();
  if (loading)
    return (
      <main className="loading">
        <Spinner /> Memeriksa sesi…
      </main>
    );
  if (!session)
    return (
      <Navigate
        to={`${role === "Provider" ? "/provider/login" : "/admin/login"}?returnTo=${encodeURIComponent(location.pathname)}`}
        replace
      />
    );
  if (role && session.user.role !== role)
    return (
      <Navigate
        to={
          session.user.role === "Provider"
            ? "/provider/dashboard"
            : session.user.role === "Customer"
              ? "/"
              : "/admin"
        }
        replace
      />
    );
  if (session.user.role === "Customer") return <Navigate to="/" replace />;
  if (!role && session.user.role === "Provider")
    return <Navigate to="/provider/dashboard" replace />;
  if (platform && session.user.role !== "PlatformAdmin")
    return <Navigate to="/admin" replace />;
  return <Outlet />;
}
