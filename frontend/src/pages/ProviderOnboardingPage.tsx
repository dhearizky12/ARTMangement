import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ProviderLayout } from "../components/provider/ProviderLayout";
import { Badge, Button, Card } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { Check } from "lucide-react";
import {
  DocumentsForm,
  PersonalForm,
  ProfileForm,
  ProviderAddressForm,
} from "../components/admin/ProviderForms";
import {
  providerApi,
  type ApplicationSection,
  type ProviderApplication,
} from "../api/marketplaceApi";

const steps = [
  { id: "personal", label: "Personal", Form: PersonalForm },
  { id: "address", label: "Alamat", Form: ProviderAddressForm },
  { id: "documents", label: "Dokumen", Form: DocumentsForm },
  { id: "profile", label: "Layanan", Form: ProfileForm },
] as const;

const statusLabels: Record<ApplicationSection["status"], string> = {
  belum: "Belum diisi",
  sebagian: "Sebagian",
  lengkapi: "Lengkap",
};

export function ProviderOnboardingPage() {
  const application = useResource(providerApi.application, "provider-application");
  const [params, setParams] = useSearchParams();
  const urlStep = params.get("step");
  const [step, setStep] = useState<string>("personal");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const data = application.data;
  const dataStep = data?.canEdit
    ? data.step === "verify"
      ? "profile"
      : data.step
    : null;
  useEffect(() => {
    if (!urlStep && dataStep) setStep(dataStep);
  }, [urlStep, dataStep]);

  function goTo(next: string) {
    setStep(next);
    setParams({ step: next }, { replace: true });
  }

  async function save(currentStep: string, body: unknown) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      if (currentStep === "personal" || currentStep === "personal-info")
        await providerApi.personal(body);
      if (currentStep === "address") await providerApi.address(body);
      if (currentStep === "profile") await providerApi.profileDetails(body);
      if (currentStep === "documents")
        await providerApi.documents(body as FormData);
      const fresh = await application.reload();
      const firstIncomplete = fresh?.sections.find(
        (s) => s.status !== "lengkapi",
      );
      goTo(firstIncomplete ? firstIncomplete.id : "profile");
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

  const currentStep = steps.some((item) => item.id === urlStep)
    ? (urlStep as string)
    : step;
  const current = steps.find((item) => item.id === currentStep) || steps[0];

  return (
    <ProviderLayout>
      <Link className="text-link" to="/provider/dashboard">
        ← Kembali ke panel
      </Link>
      <p className="eyebrow">PENDAFTARAN PENYEDIA JASA</p>
      <h1>Lengkapi profil Anda</h1>
      <ResourceState {...application} />
      {data && (
        <ApplicationEditor
          data={data}
          current={current}
          goTo={goTo}
          save={save}
          submit={submit}
          busy={busy}
          message={message}
          error={error}
        />
      )}
    </ProviderLayout>
  );
}

function ApplicationEditor({
  data,
  current,
  goTo,
  save,
  submit,
  busy,
  message,
  error,
}: {
  data: ProviderApplication;
  current: (typeof steps)[number];
  goTo: (step: string) => void;
  save: (step: string, body: unknown) => Promise<void>;
  submit: () => Promise<void>;
  busy: boolean;
  message: string;
  error: string;
}) {
  const canEdit = data.canEdit;
  const sections = data.sections;
  const missing = steps.filter(
    (s) => sections.find((x) => x.id === s.id)?.status !== "lengkapi",
  );
  const allComplete = missing.length === 0;
  return (
    <>
      <ul className="step-tabs" role="tablist" aria-label="Tahap pendaftaran Provider">
        {steps.map((item) => {
          const section = sections.find((x) => x.id === item.id);
          const status = section?.status ?? "belum";
          const selected = item.id === current.id;
          return (
            <li key={item.id}>
              <button
                type="button"
                role="tab"
                aria-selected={selected}
                className={`${status === "lengkapi" ? "complete" : ""}`}
                disabled={busy}
                onClick={() => goTo(item.id)}
              >
                <span>
                  {status === "lengkapi" ? (
                    <Check size={18} aria-hidden="true" />
                  ) : (
                    steps.indexOf(item) + 1
                  )}
                  {item.label}
                </span>
                <span className="step-tab-status">
                  {statusLabels[status]}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
      <Card lifted>
        <div className="section-title">
          <h2>{current.label}</h2>
          <Badge tone={data.status === "Approved" ? "success" : "accent"}>
            {data.status}
          </Badge>
        </div>
        {data.note && <p className="error">Catatan admin: {data.note}</p>}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        {message && <p role="status">{message}</p>}
        {canEdit ? (
          <current.Form
            data={data.provider}
            save={save}
            busy={busy}
            downloadDocument={providerApi.downloadDocument}
          />
        ) : (
          <p>
            Aplikasi sedang ditinjau. Anda dapat kembali ke panel untuk melihat
            status terbaru.
          </p>
        )}
        {canEdit && (
          <div className="action-stack">
            <Button
              type="button"
              variant="secondary"
              className="wide"
              disabled={busy || !allComplete}
              onClick={() => void submit()}
            >
              {busy ? "Mengirim…" : "Kirim aplikasi"}
            </Button>
            {!allComplete && (
              <p className="submit-hint">
                Belum lengkap:{" "}
                {missing.map((m, i) => (
                  <span key={m.id}>
                    {i > 0 && ", "}
                    <Link to={`/provider/onboarding?step=${m.id}`}>
                      {m.label}
                    </Link>
                  </span>
                ))}
                .
              </p>
            )}
          </div>
        )}
      </Card>
    </>
  );
}
