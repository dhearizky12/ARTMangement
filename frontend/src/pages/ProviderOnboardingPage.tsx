import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ProviderLayout } from "../components/provider/ProviderLayout";
import { Badge, Button, Card } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import {
  DocumentsForm,
  PersonalForm,
  ProfileForm,
  ProviderAddressForm,
} from "../components/admin/ProviderForms";
import { providerApi, type ProviderApplication } from "../api/marketplaceApi";

const steps = [
  { id: "personal", label: "Personal", Form: PersonalForm },
  { id: "address", label: "Alamat", Form: ProviderAddressForm },
  { id: "documents", label: "Dokumen", Form: DocumentsForm },
  { id: "profile", label: "Layanan", Form: ProfileForm },
] as const;

export function ProviderOnboardingPage() {
  const application = useResource(providerApi.application, "provider-application");
  const [step, setStep] = useState("personal");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const data = application.data;
  const current = steps.find((item) => item.id === step) || steps[0];
  useEffect(() => {
    if (data?.canEdit) setStep(data.step === "verify" ? "profile" : data.step);
  }, [data?.canEdit, data?.step]);

  async function save(currentStep: string, body: unknown) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (currentStep === "personal") await providerApi.personal(body);
      if (currentStep === "address") await providerApi.address(body);
      if (currentStep === "profile") await providerApi.profileDetails(body);
      if (currentStep === "documents") await providerApi.documents(body as FormData);
      await application.reload();
      setStep(currentStep === "personal" ? "address" : currentStep === "address" ? "documents" : currentStep === "documents" ? "profile" : "profile");
      setMessage("Data tersimpan.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Data gagal disimpan.");
    } finally {
      setBusy(false);
    }
  }

  async function submit() {
    setBusy(true);
    setError("");
    try {
      await providerApi.submit();
      await application.reload();
      setMessage("Aplikasi berhasil dikirim dan sedang ditinjau admin.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Aplikasi gagal dikirim.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <ProviderLayout>
      <Link className="text-link" to="/provider/dashboard">← Kembali ke panel</Link>
      <p className="eyebrow">PENDAFTARAN PENYEDIA JASA</p>
      <h1>Lengkapi profil Anda</h1>
      <ResourceState {...application} />
      {data && <ApplicationEditor data={data} step={step} setStep={setStep} current={current} save={save} submit={submit} busy={busy} message={message} error={error} />}
    </ProviderLayout>
  );
}

function ApplicationEditor({
  data,
  step,
  setStep,
  current,
  save,
  submit,
  busy,
  message,
  error,
}: {
  data: ProviderApplication;
  step: string;
  setStep: (step: string) => void;
  current: (typeof steps)[number];
  save: (step: string, body: unknown) => Promise<void>;
  submit: () => Promise<void>;
  busy: boolean;
  message: string;
  error: string;
}) {
  const currentIndex = steps.findIndex((item) => item.id === step);
  const reachedIndex = data.step === "verify" ? steps.length - 1 : Math.max(0, steps.findIndex((item) => item.id === data.step));
  const canEdit = data.canEdit;
  return (
    <>
      <div className="wizard-progress" aria-label="Tahap pendaftaran Provider">
        {steps.map((item, index) => (
          <Button key={item.id} variant={item.id === step ? "secondary" : "ghost"} disabled={!canEdit || busy || index > reachedIndex} onClick={() => setStep(item.id)} aria-current={item.id === step ? "step" : undefined}>
            {index + 1}. {item.label}
          </Button>
        ))}
      </div>
      <Card lifted>
        <div className="section-title">
          <h2>{current.label}</h2>
          <Badge tone={data.status === "Approved" ? "success" : "accent"}>{data.status}</Badge>
        </div>
        {data.note && <p className="error">Catatan admin: {data.note}</p>}
        {error && <p className="error" role="alert">{error}</p>}
        {message && <p role="status">{message}</p>}
        {canEdit ? (
          <current.Form data={data.provider} save={save} busy={busy} downloadDocument={providerApi.downloadDocument} />
        ) : (
          <p>Aplikasi sedang ditinjau. Anda dapat kembali ke panel untuk melihat status terbaru.</p>
        )}
        {canEdit && data.step === "verify" && currentIndex === steps.length - 1 && (
          <Button type="button" variant="secondary" disabled={busy} onClick={() => void submit()}>
            {busy ? "Mengirim…" : "Kirim aplikasi untuk ditinjau"}
          </Button>
        )}
      </Card>
    </>
  );
}
