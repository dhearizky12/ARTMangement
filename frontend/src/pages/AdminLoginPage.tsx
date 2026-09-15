import { useState, type FormEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { ArrowLeft, ArrowRight } from "lucide-react";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { AuthLayout } from "../components/AuthLayout";
import { Badge, Button, Input } from "../components/ui";
export function AdminLoginPage() {
  const { session, loading } = useAuth();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  if (session)
    return (
      <Navigate
        to={session.user.role === "Admin" ? "/admin" : "/dashboard"}
        replace
      />
    );
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await authApi.admin(
        String(data.get("email")),
        String(data.get("password")),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Login gagal.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <AuthLayout>
      <div className="form-card">
        <Badge tone="accent">AREA ADMINISTRATOR</Badge>
        <h2>Masuk ke ruang kelola.</h2>
        <p className="muted">Gunakan akun administrator yang terdaftar.</p>
        <form onSubmit={submit}>
          <fieldset disabled={busy || loading}>
            <Input
              label="Email"
              name="email"
              type="email"
              autoComplete="username"
              required
              maxLength={254}
            />
            <Input
              label="Kata sandi"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={256}
            />
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            <Button type="submit" className="wide" disabled={busy || loading}>
              {loading
                ? "Memeriksa sesi…"
                : busy
                  ? "Memeriksa akun…"
                  : "Masuk sebagai admin"}
              <ArrowRight aria-hidden="true" />
            </Button>
          </fieldset>
        </form>
        <Link className="text-link" to="/login">
          <ArrowLeft aria-hidden="true" />
          Kembali ke login pengguna
        </Link>
      </div>
    </AuthLayout>
  );
}
