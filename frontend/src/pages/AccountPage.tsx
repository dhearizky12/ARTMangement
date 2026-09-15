import { LogOut, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { authApi } from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import { useProfile } from "../routes/ProfileBoundary";
import { AppShell } from "../components/AppShell";
import { Badge, Button, Card } from "../components/ui";
export function AccountPage() {
  const { session } = useAuth();
  const { status } = useProfile();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <AppShell>
      <p className="eyebrow">RUANG PRIBADIMU</p>
      <h1>Profil saya.</h1>
      <Card className="account-card">
        <Badge tone="success">Profil lengkap</Badge>
        <h2>{session!.user.fullName}</h2>
        <p>{session!.user.email}</p>
        <dl>
          <dt>Nomor HP</dt>
          <dd>{status.personalInfo?.phoneNumber || "—"}</dd>
          <dt>Alamat</dt>
          <dd>
            {status.address
              ? `${status.address.addressLine}, ${status.address.villageName ? status.address.villageName + ", " : ""}${status.address.district}, ${status.address.city}, ${status.address.province} ${status.address.postalCode}`
              : "—"}
          </dd>
          <dt>Dokumen identitas</dt>
          <dd>
            {status.document?.documentType || "—"} ·{" "}
            {status.document?.verificationStatus === "Pending"
              ? "Menunggu pemeriksaan"
              : status.document?.verificationStatus || "—"}
          </dd>
        </dl>
        <p className="privacy-note">
          <ShieldCheck aria-hidden="true" /> Dokumen disimpan privat. Profil
          lengkap tidak berarti identitas sudah terverifikasi.
        </p>
      </Card>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <Button
        variant="ghost"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          try {
            await authApi.logout();
          } catch {
            setError("Gagal keluar. Coba lagi.");
          } finally {
            setBusy(false);
          }
        }}
      >
        <LogOut aria-hidden="true" />
        {busy ? "Keluar…" : "Keluar dari akun"}
      </Button>
    </AppShell>
  );
}
