import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { authApi } from "../api/authApi";
import { AppShell } from "../components/AppShell";
import { Card, Button } from "../components/ui";
import { GoogleLogo } from "../components/GoogleLogo";
import { useState } from "react";
export function AccountPage() {
  const { session } = useAuth();
  const [error, setError] = useState("");
  return (
    <AppShell>
      <h1>Akun saya</h1>
      <Card>
        {session ? (
          <>
            <h2>{session.user.fullName}</h2>
            <p>{session.user.email}</p>
            <p>{session.user.role}</p>
            <div className="account-login-actions">
              <Button
                variant="secondary"
                className="btn btn-secondary wide"
                onClick={() =>
                  authApi.logout().catch((e) => setError(e.message))
                }
              >
                Keluar
              </Button>
              {session.user.role === "Provider" ? (
                <Link className="text-link" to="/provider/dashboard">
                  Buka panel Provider
                </Link>
              ) : session.user.role !== "Customer" ? (
                <Link className="text-link" to="/admin">
                  Buka ruang kelola
                </Link>
              ) : null}
              {error && <p role="alert">{error}</p>}
            </div>
          </>
        ) : (
          <>
            <p>Masuk saat Anda siap memesan bantuan.</p>
            <div className="account-login-actions">
              <Link
                className="btn btn-primary wide"
                to="/login?returnTo=%2Faccount"
              >
                <span className="google-tile">
                  <GoogleLogo />
                </span>
                Masuk dengan Google
              </Link>
              <div className="provider-divider">
                <span>Untuk penyedia jasa</span>
              </div>
              <Link className="btn btn-ghost wide" to="/provider/login">
                Login penyedia jasa
              </Link>
              <Link className="btn btn-ghost wide" to="/provider/register">
                Daftar sebagai penyedia jasa
              </Link>
            </div>
            <p className="admin-link">
              <Link to="/admin/login">Login admin</Link>
            </p>
          </>
        )}
      </Card>
    </AppShell>
  );
}
