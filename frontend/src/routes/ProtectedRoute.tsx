import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
export function ProtectedRoute({ platform = false }: { platform?: boolean }) {
  const { session, loading } = useAuth();
  const location = useLocation();
  if (loading) return <main className="loading">Memeriksa sesi…</main>;
  if (!session)
    return (
      <Navigate
        to={`/admin/login?returnTo=${encodeURIComponent(location.pathname)}`}
        replace
      />
    );
  if (session.user.role === "Customer") return <Navigate to="/" replace />;
  if (platform && session.user.role !== "PlatformAdmin")
    return <Navigate to="/admin" replace />;
  return <Outlet />;
}
