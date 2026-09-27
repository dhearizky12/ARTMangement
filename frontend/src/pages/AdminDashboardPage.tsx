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
  const applications = useResource(adminApi.applications, "provider-applications");
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
  const [moderating, setModerating] = useState("");
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
  async function moderate(id: string, action: "approve" | "reject" | "request-changes" | "suspend") {
    const note = action === "approve" ? undefined : window.prompt("Catatan untuk Provider:");
    if (action !== "approve" && note === null) return;
    setModerating(id);
    setError("");
    try {
      await adminApi.moderate(id, action, note || undefined);
      await Promise.all([applications.reload(), roster.reload()]);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Moderasi gagal.");
    } finally {
      setModerating("");
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
      <Card>
        <div className="section-title">
          <h2>Aplikasi Provider</h2>
          <Badge tone="accent">Menunggu moderasi</Badge>
        </div>
        <ResourceState {...applications} />
        {applications.data?.length ? applications.data.map(({ provider: p, step }) => (
          <div className="moderation-row" key={p.id}>
            <div>
              <h3>{p.fullName || "Profil belum lengkap"}</h3>
              <p className="muted">Tahap: {step} · {p.applicationStatus}</p>
            </div>
            <div className="filter-chips">
              <Link className="btn btn-ghost" to={`/admin/providers/${p.id}`}>Periksa</Link>
              <Button disabled={moderating === p.id} onClick={() => void moderate(p.id, "approve")}>Setujui</Button>
              <Button variant="secondary" disabled={moderating === p.id} onClick={() => void moderate(p.id, "request-changes")}>Minta perbaikan</Button>
              <Button variant="ghost" disabled={moderating === p.id} onClick={() => void moderate(p.id, "reject")}>Tolak</Button>
            </div>
          </div>
        )) : applications.data ? <p>Tidak ada aplikasi yang menunggu moderasi.</p> : null}
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
