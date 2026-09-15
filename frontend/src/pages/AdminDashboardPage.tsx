import { useEffect, useState } from "react";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { Brand } from "../components/Brand";
import { Badge, Button, Card } from "../components/ui";
export function AdminDashboardPage() {
  const { session } = useAuth();
  const [message, setMessage] = useState("Memeriksa akses…");
  const [error, setError] = useState("");
  useEffect(() => {
    authApi
      .get<{ message: string }>("/api/admin/dashboard")
      .then((r) => setMessage(r.message))
      .catch((e) => setError(e.message));
  }, []);
  return (
    <main className="page">
      <header className="admin-header">
        <Brand />
        <Button
          variant="ghost"
          onClick={() =>
            authApi.logout().catch(() => setError("Gagal keluar. Coba lagi."))
          }
        >
          Keluar
        </Button>
      </header>
      <Badge tone="accent">ADMINISTRATOR</Badge>
      <h1>Halo, {session!.user.fullName}.</h1>
      <Card>
        <h2>Ruang kelola Bantu-Bantu</h2>
        <p>{message}</p>
        <p className="muted">
          Pengelolaan penyedia dan pemeriksaan dokumen akan tersedia pada
          pengembangan berikutnya.
        </p>
      </Card>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </main>
  );
}
