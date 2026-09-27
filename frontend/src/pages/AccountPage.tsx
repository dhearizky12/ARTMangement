import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { authApi } from "../api/authApi";
import { AppShell } from "../components/AppShell";
import { Card, Button } from "../components/ui";
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
            {session.user.role === "Provider" ? (
              <Link className="text-link" to="/provider/dashboard">Buka panel Provider</Link>
            ) : session.user.role !== "Customer" ? (
              <Link className="text-link" to="/admin">Buka ruang kelola</Link>
            ) : null}
            <Button
              variant="secondary"
              onClick={() => authApi.logout().catch((e) => setError(e.message))}
            >
              Keluar
            </Button>
            {error && <p role="alert">{error}</p>}
          </>
        ) : (
          <>
            <p>Masuk saat Anda siap memesan bantuan.</p>
            <div className="account-login-actions">
              <Link
                className="btn btn-primary wide"
                to="/login?returnTo=%2Faccount"
              >
                Masuk dengan Google
              </Link>
              <Link className="text-link" to="/admin/login">
                Login admin
              </Link>
              <Link className="text-link" to="/provider/login">
                Login penyedia jasa
              </Link>
              <Link className="text-link" to="/provider/register">
                Daftar sebagai penyedia jasa
              </Link>
            </div>
          </>
        )}
      </Card>
    </AppShell>
  );
}
