import { useState, type FormEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { AuthLayout } from "../components/AuthLayout";
import { Badge, Button, Input } from "../components/ui";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";

export function ProviderRegisterPage() {
  const { session, loading } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  if (session) {
    return <Navigate to={session.user.role === "Provider" ? "/provider/onboarding" : "/"} replace />;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await authApi.registerProvider(
        String(data.get("email")),
        String(data.get("password")),
        String(data.get("confirmPassword")),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Pendaftaran gagal.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout>
      <div className="form-card">
        <Badge tone="success">PENYEDIA JASA</Badge>
        <h2>Mulai bantu lebih banyak orang.</h2>
        <p className="muted">Buat akun Provider, lengkapi profil, lalu tunggu proses moderasi admin.</p>
        <form onSubmit={submit}>
          <fieldset disabled={busy || loading}>
            <Input label="Email" name="email" type="email" autoComplete="username" required maxLength={254} />
            <Input label="Kata sandi" name="password" type="password" autoComplete="new-password" required minLength={14} maxLength={256} />
            <Input label="Konfirmasi kata sandi" name="confirmPassword" type="password" autoComplete="new-password" required minLength={14} maxLength={256} />
            {error && <p className="error" role="alert">{error}</p>}
            <Button type="submit" className="wide" disabled={busy || loading}>
              {busy ? "Membuat akun…" : "Daftar sebagai Provider"}
            </Button>
          </fieldset>
        </form>
        <div className="login-actions">
          <Link className="text-link" to="/provider/login">Sudah punya akun Provider?</Link>
          <Link className="text-link" to="/login">Login sebagai Customer</Link>
        </div>
      </div>
    </AuthLayout>
  );
}
