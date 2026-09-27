import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { AdminLayout } from "../components/admin/AdminLayout";
import { Card, Button, Input, Select, Badge } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { adminApi, type Agency } from "../api/marketplaceApi";
import { useAuth } from "../context/AuthContext";
export function AdminDashboardPage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const roster = useResource(adminApi.roster, "roster");
  const agencies = useResource(
    () =>
      session?.user.role === "PlatformAdmin"
        ? adminApi.agencies()
        : Promise.resolve([] as Agency[]),
    "roster-agencies",
  );
  const [agencyId, setAgencyId] = useState("");
  const [providerEmail, setProviderEmail] = useState("");
  const [providerPassword, setProviderPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function create() {
    setBusy(true);
    setError("");
    try {
      const draft = await adminApi.draft(
        agencyId || null,
        providerEmail,
        providerPassword,
      );
      navigate(`/admin/providers/${draft.provider.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal membuat draft.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <AdminLayout>
      <h1>Kelola penyedia jasa</h1>
      <Card>
        <h2>Daftarkan penyedia</h2>
        {session?.user.role === "PlatformAdmin" ? (
          <Select
            label="Pengelola"
            value={agencyId}
            onChange={(e) => setAgencyId(e.target.value)}
          >
            <option value="">Bantu-Bantu direct talent</option>
            {agencies.data
              ?.filter((a) => a.status === "Approved")
              .map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
          </Select>
        ) : (
          <p>Penyedia baru otomatis masuk ke roster agency Anda.</p>
        )}
        <Input
          label="Email login Provider"
          type="email"
          value={providerEmail}
          onChange={(e) => setProviderEmail(e.target.value)}
          autoComplete="off"
          required
        />
        <Input
          label="Kata sandi awal Provider"
          type="password"
          value={providerPassword}
          onChange={(e) => setProviderPassword(e.target.value)}
          minLength={14}
          maxLength={256}
          autoComplete="new-password"
          required
        />
        <Button
          disabled={busy || !providerEmail || providerPassword.length < 14}
          onClick={create}
        >
          {busy ? "Membuat…" : "Buat draft penyedia"}
        </Button>
        {error && <p role="alert">{error}</p>}
      </Card>
      <ResourceState {...roster} />
      <div className="provider-grid">
        {roster.data?.map(({ provider: p, step }) => (
          <Card key={p.id}>
            <Badge>{p.verificationStatus}</Badge>
            <h2>{p.fullName || "Draft belum diberi nama"}</h2>
            <p>{p.agencyName || "Bantu-Bantu direct talent"}</p>
            <p>Tahap: {step}</p>
            <Link className="btn btn-secondary" to={`/admin/providers/${p.id}`}>
              Kelola penyedia
            </Link>
          </Card>
        ))}
      </div>
      {roster.data?.length === 0 && <p>Belum ada penyedia dalam roster ini.</p>}
    </AdminLayout>
  );
}
