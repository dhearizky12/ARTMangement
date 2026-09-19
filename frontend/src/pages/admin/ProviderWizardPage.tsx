import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card, Button, Badge } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import { adminApi, type ProviderAdmin } from "../../api/marketplaceApi";
import {
  PersonalForm,
  ProviderAddressForm,
  DocumentsForm,
  ProfileForm,
  VerificationForm,
} from "../../components/admin/ProviderForms";
const steps = [
  { id: "personal", label: "Personal", Form: PersonalForm },
  { id: "address", label: "Alamat", Form: ProviderAddressForm },
  { id: "documents", label: "Dokumen", Form: DocumentsForm },
  { id: "profile", label: "Layanan", Form: ProfileForm },
  { id: "verify", label: "Verifikasi", Form: VerificationForm },
] as const;
function Editor({
  data,
  reload,
}: {
  data: ProviderAdmin;
  reload: () => Promise<void>;
}) {
  const [step, setStep] = useState(data.step);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const current = steps.find((s) => s.id === step)!;
  async function save(endpoint: string, body: unknown) {
    setBusy(true);
    setError("");
    setSaved(false);
    try {
      const updated = await adminApi.saveProvider(
        data.provider.id,
        endpoint,
        body,
      );
      await reload();
      setStep(updated.step);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Data gagal disimpan.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="wizard-progress" aria-label="Tahap pendaftaran">
        {steps.map((s, i) => (
          <Button
            key={s.id}
            variant={s.id === step ? "secondary" : "ghost"}
            disabled={busy || i > steps.findIndex((x) => x.id === data.step)}
            onClick={() => setStep(s.id)}
            aria-current={s.id === step ? "step" : undefined}
          >
            {i + 1}. {s.label}
          </Button>
        ))}
      </div>
      <Card lifted>
        <h2>{current.label}</h2>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        {saved && <p role="status">Data tersimpan.</p>}
        <current.Form
          key={step + JSON.stringify(data)}
          data={data}
          save={save}
          busy={busy}
        />
      </Card>
    </>
  );
}
export function ProviderWizardPage() {
  const { id } = useParams();
  const result = useResource(() => adminApi.provider(id!), id!);
  return (
    <AdminLayout>
      <Link className="text-link" to="/admin">
        ← Kembali ke roster
      </Link>
      <h1>{result.data?.provider.fullName || "Daftarkan penyedia"}</h1>
      {result.data && <Badge>{result.data.provider.verificationStatus}</Badge>}
      <ResourceState {...result} />
      {result.data && <Editor data={result.data} reload={result.reload} />}
    </AdminLayout>
  );
}
