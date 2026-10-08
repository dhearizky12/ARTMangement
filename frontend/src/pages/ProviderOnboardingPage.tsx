import { useEffect, useLayoutEffect, useRef, useState } from "react";
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
  isSectionDirty,
} from "../components/admin/ProviderForms";
import {
  providerApi,
  type ApplicationSection,
  type ProviderApplication,
} from "../api/marketplaceApi";
import {
  loadDrafts,
  saveDrafts,
  type Drafts,
  type SectionId,
} from "../lib/draftStorage";

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

const sectionBySaveStep: Record<string, SectionId> = {
  "personal-info": "personal",
  personal: "personal",
  address: "address",
  documents: "documents",
  profile: "profile",
};

export function ProviderOnboardingPage() {
  const application = useResource(
    providerApi.application,
    "provider-application",
  );
  const [params, setParams] = useSearchParams();
  const urlStep = params.get("step");
  const [step, setStep] = useState<string>("personal");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const data = application.data;
  const providerId = data?.provider.provider.id ?? "";
  const draftsRef = useRef<Drafts>({});
  const [drafts, setDrafts] = useState<Drafts>({});
  const [hydrated, setHydrated] = useState(false);

  useLayoutEffect(() => {
    if (!providerId) return;
    const loaded = loadDrafts(providerId);
    draftsRef.current = loaded;
    setDrafts(loaded);
    setHydrated(true);
  }, [providerId]);

  function persist(next: Drafts) {
    if (providerId) saveDrafts(providerId, next);
  }
  function onDraftChange(id: SectionId, draft: Drafts[SectionId]) {
    const next = { ...draftsRef.current, [id]: draft } as Drafts;
    draftsRef.current = next;
    setDrafts(next);
    persist(next);
  }
  function clearDraft(id: SectionId) {
    const next = { ...draftsRef.current };
    delete next[id];
    draftsRef.current = next;
    setDrafts(next);
    persist(next);
  }
  function clearAllDrafts() {
    const next: Drafts = {};
    draftsRef.current = next;
    setDrafts(next);
    persist(next);
  }

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
      const section = sectionBySaveStep[currentStep];
      if (section) clearDraft(section);
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
      clearAllDrafts();
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
      {data && hydrated && (
        <ApplicationEditor
          data={data}
          current={current}
          goTo={goTo}
          save={save}
          submit={submit}
          busy={busy}
          message={message}
          error={error}
          drafts={drafts}
          onDraftChange={onDraftChange}
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
  drafts,
  onDraftChange,
}: {
  data: ProviderApplication;
  current: (typeof steps)[number];
  goTo: (step: string) => void;
  save: (step: string, body: unknown) => Promise<void>;
  submit: () => Promise<void>;
  busy: boolean;
  message: string;
  error: string;
  drafts: Drafts;
  onDraftChange: (id: SectionId, draft: Drafts[SectionId]) => void;
}) {
  const activeTabRef = useRef<HTMLButtonElement | null>(null);
  const canEdit = data.canEdit;
  const sections = data.sections;
  const missing = steps.filter(
    (s) => sections.find((x) => x.id === s.id)?.status !== "lengkapi",
  );
  const allComplete = missing.length === 0;
  const anyDirty = steps.some((s) =>
    isSectionDirty(s.id, drafts[s.id], data.provider),
  );

  useEffect(() => {
    activeTabRef.current?.scrollIntoView({
      block: "nearest",
      inline: "nearest",
    });
  }, [current.id]);

  useEffect(() => {
    if (!canEdit || !anyDirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    const onClick = (e: MouseEvent) => {
      if (
        e.defaultPrevented ||
        e.button !== 0 ||
        e.metaKey ||
        e.ctrlKey ||
        e.shiftKey ||
        e.altKey
      )
        return;
      const target = e.target as Element | null;
      const anchor = target?.closest?.("a");
      if (!anchor) return;
      const href = anchor.getAttribute("href");
      if (!href || href.startsWith("#")) return;
      let url: URL;
      try {
        url = new URL(anchor.href, window.location.href);
      } catch {
        return;
      }
      if (url.origin !== window.location.origin) return;
      if (url.pathname === window.location.pathname) return;
      if (
        !window.confirm(
          "Ada perubahan yang belum disimpan. Yakin ingin meninggalkan halaman?",
        )
      ) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    document.addEventListener("click", onClick, true);
    return () => {
      window.removeEventListener("beforeunload", onBeforeUnload);
      document.removeEventListener("click", onClick, true);
    };
  }, [canEdit, anyDirty]);

  return (
    <>
      <ul
        className="step-tabs"
        role="tablist"
        aria-label="Tahap pendaftaran Provider"
      >
        {steps.map((item) => {
          const section = sections.find((x) => x.id === item.id);
          const status = section?.status ?? "belum";
          const selected = item.id === current.id;
          const dirty = isSectionDirty(item.id, drafts[item.id], data.provider);
          return (
            <li key={item.id}>
              <button
                ref={selected ? activeTabRef : undefined}
                type="button"
                role="tab"
                aria-selected={selected}
                className={`${status === "lengkapi" ? "complete" : ""}`}
                disabled={busy}
                onClick={() => goTo(item.id)}
              >
                <span className="step-tab-num">
                  {status === "lengkapi" ? (
                    <Check size={16} aria-hidden="true" />
                  ) : (
                    steps.indexOf(item) + 1
                  )}
                </span>
                <span className="step-tab-label">{item.label}</span>
                <span className="step-tab-status">{statusLabels[status]}</span>
                {dirty && (
                  <span className="step-tab-dirty">belum disimpan</span>
                )}
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
            draft={drafts[current.id]}
            onDraftChange={onDraftChange}
          />
        ) : (
          <p>
            Aplikasi sedang ditinjau. Anda dapat kembali ke panel untuk melihat
            status terbaru.
          </p>
        )}
        {canEdit && (
          <div className="submit-block">
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
          </div>
        )}
      </Card>
    </>
  );
}
