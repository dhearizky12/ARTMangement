import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import type { Role } from "../api/authApi";
export function ProtectedRoute({ role }: { role: Role }) {
  const { session, loading } = useAuth();
  if (loading) return <main className="loading">Memeriksa sesi…</main>;
  if (!session)
    return (
      <Navigate to={role === "Admin" ? "/admin/login" : "/login"} replace />
    );
  if (session.user.role !== role)
    return (
      <Navigate
        to={session.user.role === "Admin" ? "/admin" : "/dashboard"}
        replace
      />
    );
  return <Outlet />;
}
