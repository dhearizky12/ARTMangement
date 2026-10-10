import { useState, type FormEvent } from "react";
import { ArrowRight } from "lucide-react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import {
  authApi,
  type TwoFactorLoginChallenge,
} from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { AuthLayout } from "../components/AuthLayout";
import { Badge, Button, Input, PasswordInput, Spinner } from "../components/ui";

export function ProviderLoginPage() {
  const [params] = useSearchParams();
  const requested = params.get("returnTo") || "/provider/dashboard";
  const returnTo = /^\/provider(?:\/|$)/.test(requested)
    ? requested
    : "/provider/dashboard";
  const { session, loading } = useAuth();
  const [challenge, setChallenge] = useState<TwoFactorLoginChallenge | null>(
    null,
  );
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [resending, setResending] = useState(false);

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
      const result = await authApi.provider(
        String(data.get("email")),
        String(data.get("password")),
      );
      if ("challengeId" in result) setChallenge(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Login Provider gagal.");
    } finally {
      setBusy(false);
    }
  }

  async function verify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!challenge) return;
    setBusy(true);
    setError("");
    try {
      await authApi.verifyTwoFactor(challenge.challengeId, code.trim());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Kode gagal diverifikasi.");
    } finally {
      setBusy(false);
    }
  }

  async function resend() {
    if (!challenge) return;
    setResending(true);
    setError("");
    try {
      await authApi.resendTwoFactor(challenge.challengeId);
      setCode("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Kode gagal dikirim ulang.");
    } finally {
      setResending(false);
    }
  }

  async function skip() {
    if (!challenge) return;
    setBusy(true);
    setError("");
    try {
      await authApi.skipTwoFactor(challenge.challengeId);
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Masuk dengan kata sandi gagal.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout>
      <div className="form-card">
        <Badge tone="success">AREA PROVIDER</Badge>
        {!challenge ? (
          <>
            <h2>Masuk ke ruang kerja Anda.</h2>
            <p className="muted">
              Gunakan email dan kata sandi yang diberikan admin.
            </p>
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
                <PasswordInput
                  label="Kata sandi"
                  name="password"
                  autoComplete="current-password"
                  required
                  maxLength={256}
                />
                {error && (
                  <p className="error" role="alert">
                    {error}
                  </p>
                )}
                <Button
                  type="submit"
                  className="wide"
                  disabled={busy || loading}
                >
                  {(busy || loading) && <Spinner />}
                  {loading
                    ? "Memeriksa sesi…"
                    : busy
                      ? "Memeriksa akun…"
                      : "Masuk sebagai Provider"}
                  <ArrowRight aria-hidden="true" />
                </Button>
              </fieldset>
            </form>
          </>
        ) : (
          <>
            <h2>Masukkan kode verifikasi.</h2>
            <p className="muted">
              Kode 6 digit telah dikirim ke{" "}
              <strong>{challenge.maskedEmail}</strong>.
            </p>
            <form onSubmit={verify}>
              <fieldset disabled={busy || loading}>
                <Input
                  label="Kode verifikasi"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  autoFocus
                  required
                  minLength={6}
                  maxLength={6}
                  pattern="[0-9]*"
                />
                {error && (
                  <p className="error" role="alert">
                    {error}
                  </p>
                )}
                <Button
                  type="submit"
                  className="wide"
                  disabled={busy || loading}
                >
                  {(busy || loading) && <Spinner />}
                  {busy ? "Memverifikasi…" : "Verifikasi kode"}
                  <ArrowRight aria-hidden="true" />
                </Button>
              </fieldset>
            </form>
            <div className="login-actions">
              <button
                type="button"
                className="text-link"
                disabled={resending || busy}
                onClick={() => void resend()}
              >
                {resending ? "Mengirim ulang…" : "Kirim ulang kode"}
              </button>
              {challenge.fallbackAllowed && (
                <button
                  type="button"
                  className="text-link"
                  disabled={busy}
                  onClick={() => void skip()}
                >
                  Masuk dengan kata sandi sekarang (akses terbatas)
                </button>
              )}
              <button
                type="button"
                className="text-link"
                disabled={busy}
                onClick={() => {
                  setChallenge(null);
                  setCode("");
                  setError("");
                }}
              >
                Ganti email / kata sandi
              </button>
            </div>
          </>
        )}
        <div className="login-actions">
          <Link className="text-link" to="/provider/register">
            Daftar sebagai Provider
          </Link>
          <Link className="text-link" to="/login">
            Login sebagai Customer
          </Link>
          <Link className="text-link" to="/admin/login">
            Login admin
          </Link>
        </div>
      </div>
    </AuthLayout>
  );
}