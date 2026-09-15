import type { ReactNode } from "react";
import { Check, LockKeyhole, LogOut } from "lucide-react";
import { useState } from "react";
import { Brand } from "../../components/Brand";
import { Button, Card } from "../../components/ui";
import { useProfile } from "../../routes/ProfileBoundary";
import { authApi } from "../../api/authApi";
export function WizardLayout({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  const { status } = useProfile();
  const [error, setError] = useState("");
  const steps = [
    { key: "personal", label: "Personal", done: status.personalCompleted },
    { key: "address", label: "Alamat", done: status.addressCompleted },
    { key: "documents", label: "Dokumen", done: status.documentsCompleted },
  ];
  return (
    <div className="wizard-shell">
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <Button
            variant="ghost"
            onClick={() =>
              authApi.logout().catch(() => setError("Gagal keluar. Coba lagi."))
            }
          >
            <LogOut aria-hidden="true" />
            Keluar
          </Button>
        </div>
      </header>
      <main className="page narrow">
        <p className="eyebrow">SEDIKIT KENAL, LEBIH NYAMAN</p>
        <h1>Lengkapi profilmu.</h1>
        <p className="muted">
          Tiga langkah singkat sebelum menjelajahi layanan.
        </p>
        <ol className="stepper" aria-label="Progres kelengkapan profil">
          {steps.map((step, index) => (
            <li
              key={step.key}
              className={status.profileStep === step.key ? "active" : ""}
              aria-current={
                status.profileStep === step.key ? "step" : undefined
              }
            >
              <span>
                {step.done ? <Check aria-label="Selesai" /> : index + 1}
              </span>
              <strong>{step.label}</strong>
            </li>
          ))}
        </ol>
        <Card lifted className="wizard-card">
          <h2>{title}</h2>
          <p className="muted">{description}</p>
          {children}
        </Card>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <p className="privacy-note">
          <LockKeyhole aria-hidden="true" />
          Data profil dan dokumenmu tidak ditampilkan secara publik.
        </p>
      </main>
    </div>
  );
}
