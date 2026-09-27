import { useState, type FormEvent } from "react";
import { ArrowRight } from "lucide-react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { AuthLayout } from "../components/AuthLayout";
import { Badge, Button, Input } from "../components/ui";

export function ProviderLoginPage() {
  const [params] = useSearchParams();
  const requested = params.get("returnTo") || "/provider/dashboard";
  const returnTo = /^\/provider(?:\/|$)/.test(requested)
    ? requested
    : "/provider/dashboard";
  const { session, loading } = useAuth();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  if (session)
    return (
      <Navigate
        to={
          session.user.role === "Provider"
            ? returnTo
            : session.user.role === "Customer"
              ? "/"
              : "/admin"
        }
        replace
      />
    );

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await authApi.provider(String(data.get("email")), String(data.get("password")));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Login Provider gagal.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout>
      <div className="form-card">
        <Badge tone="success">AREA PROVIDER</Badge>
        <h2>Masuk ke ruang kerja Anda.</h2>
        <p className="muted">Gunakan email dan kata sandi yang diberikan admin.</p>
        <form onSubmit={submit}>
          <fieldset disabled={busy || loading}>
            <Input
              label="Email Provider"
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
            {error && <p className="error" role="alert">{error}</p>}
            <Button type="submit" className="wide" disabled={busy || loading}>
              {loading ? "Memeriksa sesi…" : busy ? "Memeriksa akun…" : "Masuk sebagai Provider"}
              <ArrowRight aria-hidden="true" />
            </Button>
          </fieldset>
        </form>
        <div className="login-actions">
          <Link className="text-link" to="/provider/register">Daftar sebagai Provider</Link>
          <Link className="text-link" to="/login">Login sebagai Customer</Link>
          <Link className="text-link" to="/admin/login">Login admin</Link>
        </div>
      </div>
    </AuthLayout>
  );
}
