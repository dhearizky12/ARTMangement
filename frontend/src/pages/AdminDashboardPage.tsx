import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { AdminLayout } from "../components/admin/AdminLayout";
import { Card, Button, Input, Select, Badge, Textarea } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { adminApi, type Agency, type ProviderAdmin } from "../api/marketplaceApi";
import { useAuth } from "../context/AuthContext";

function VerificationQueueItem({ item, reload }: { item: ProviderAdmin; reload: () => Promise<void> }) {
  const p = item.provider;
  const [checks, setChecks] = useState({
    identityVerified: p.identityVerified,
    backgroundCheckPassed: p.backgroundCheckPassed,
    contractSigned: p.contractSigned,
  });
  const [note, setNote] = useState(p.moderationNote ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    setChecks({ identityVerified: p.identityVerified, backgroundCheckPassed: p.backgroundCheckPassed, contractSigned: p.contractSigned });
    setNote(p.moderationNote ?? "");
  }, [p.id, p.identityVerified, p.backgroundCheckPassed, p.contractSigned, p.moderationNote]);

  async function verify(status: "Pending" | "Verified" | "Rejected") {
    if (status === "Rejected" && !note.trim()) {
      setError("Alasan penolakan wajib diisi.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await adminApi.verify(p.id, { ...checks, status, note: note.trim() || null });
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Pembaruan verifikasi gagal.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card className="verification-queue-item">
      <div className="section-title">
        <div>
          <h3>{p.fullName || "Profil belum lengkap"}</h3>
          <p className="muted">{p.agencyName || "Bantu-Bantu direct talent"} · Tahap {item.step}</p>
        </div>
        <Badge tone="accent">Pending</Badge>
      </div>
      <div className="verification-checklist" aria-label={`Checklist ${p.fullName}`}>
        <label className="check-option"><input type="checkbox" checked={checks.identityVerified} onChange={(e) => setChecks((v) => ({ ...v, identityVerified: e.target.checked }))} />Identitas telah diperiksa</label>
        <label className="check-option"><input type="checkbox" checked={checks.backgroundCheckPassed} onChange={(e) => setChecks((v) => ({ ...v, backgroundCheckPassed: e.target.checked }))} />Pemeriksaan latar belakang lulus</label>
        <label className="check-option"><input type="checkbox" checked={checks.contractSigned} onChange={(e) => setChecks((v) => ({ ...v, contractSigned: e.target.checked }))} />Kontrak telah ditandatangani</label>
      </div>
      <Textarea label="Alasan penolakan (wajib saat menolak)" value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000} placeholder="Isi jika aplikasi ditolak." />
      {error && <p className="error" role="alert">{error}</p>}
      <div className="filter-chips verification-actions">
        <Link className="btn btn-ghost" to={`/admin/providers/${p.id}`}>Periksa detail</Link>
        <Button variant="secondary" disabled={busy} onClick={() => void verify("Pending")}>Simpan checklist</Button>
        <Button disabled={busy || !checks.identityVerified || !checks.backgroundCheckPassed || !checks.contractSigned} onClick={() => void verify("Verified")}>Setujui</Button>
        <Button variant="ghost" disabled={busy || !note.trim()} onClick={() => void verify("Rejected")}>Tolak</Button>
      </div>
    </Card>
  );
}

export function AdminDashboardPage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const roster = useResource(adminApi.roster, "roster");
  const verificationQueue = useResource(adminApi.verificationQueue, "provider-verification-queue");
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
      <section id="verification-queue" aria-labelledby="verification-queue-title">
        <div className="section-title">
          <h2 id="verification-queue-title">Queue verifikasi provider</h2>
          <Badge tone="accent">Pending</Badge>
        </div>
        <ResourceState {...verificationQueue} />
        {verificationQueue.data?.length ? verificationQueue.data.map((item) => (
          <VerificationQueueItem key={item.provider.id} item={item} reload={verificationQueue.reload} />
        )) : verificationQueue.data ? <Card><p>Tidak ada provider yang menunggu verifikasi.</p></Card> : null}
      </section>
      <section id="provider-roster" aria-labelledby="provider-roster-title">
        <h2 id="provider-roster-title">Roster provider</h2>
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
      </section>
    </AdminLayout>
  );
}
