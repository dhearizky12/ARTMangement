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
            {session.user.role !== "Customer" && (
              <Link className="text-link" to="/admin">
                Buka ruang kelola
              </Link>
            )}
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
            <Link className="btn btn-primary" to="/login?returnTo=%2Faccount">
              Masuk dengan Google
            </Link>
            <Link className="text-link" to="/admin/login">
              Login admin
            </Link>
          </>
        )}
      </Card>
    </AppShell>
  );
}
