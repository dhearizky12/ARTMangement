import { useEffect, useId, useRef, useState } from "react";
import type { FormEvent } from "react";
import {
  authApi,
  type TwoFactorLoginChallenge,
} from "../../api/authApi";
import { Button, Input, Spinner } from "../ui";

export function openStepUpDialog() {
  if (typeof window !== "undefined")
    window.dispatchEvent(new Event("step-up-required"));
}

export function StepUpDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [challenge, setChallenge] = useState<TwoFactorLoginChallenge | null>(
    null,
  );
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const titleId = useId();

  async function start() {
    setBusy(true);
    setError("");
    try {
      const result = await authApi.startProviderStepUp();
      setChallenge(result);
      setCode("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memulai verifikasi.");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    if (!open) return;
    setChallenge(null);
    setCode("");
    setError("");
    setBusy(false);
    void start();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    document.addEventListener("keydown", onKey);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previousOverflow;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  function close() {
    if (busyRef.current) return;
    setChallenge(null);
    setCode("");
    setError("");
    onOpenChange(false);
  }

  async function verify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!challenge) return;
    setBusy(true);
    setError("");
    try {
      await authApi.verifyProviderStepUp(challenge.challengeId, code.trim());
      setChallenge(null);
      setCode("");
      setError("");
      onOpenChange(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Kode gagal diverifikasi.");
    } finally {
      setBusy(false);
    }
  }

  async function resend() {
    if (!challenge) return;
    setBusy(true);
    setError("");
    try {
      await authApi.resendTwoFactor(challenge.challengeId);
      setCode("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Kode gagal dikirim ulang.");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    busyRef.current = busy;
  }, [busy]);

  if (!open) return null;
  return (
    <div
      className="modal-overlay"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) close();
      }}
    >
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <h2 id={titleId}>Verifikasi email untuk melanjutkan</h2>
        <p className="muted">
          Aksi ini membutuhkan verifikasi email. Kami mengirimkan kode 6 digit
          ke{" "}
          {challenge ? (
            <strong>{challenge.maskedEmail}</strong>
          ) : (
            "email Anda"
          )}
          .
        </p>
        {!challenge && busy ? (
          <p className="muted">
            <Spinner /> Menyiapkan verifikasi…
          </p>
        ) : (
          <form onSubmit={verify}>
            <fieldset disabled={busy}>
              <Input
                label="Kode verifikasi"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                inputMode="numeric"
                autoComplete="one-time-code"
                autoFocus
                required
                minLength={6}
                maxLength={6}
                pattern="[0-9]*"
              />
              {error && (
                <p role="alert" className="error">
                  {error}
                </p>
              )}
              <div className="modal-actions">
                <button
                  type="button"
                  className="btn btn-ghost"
                  onClick={close}
                  disabled={busy}
                >
                  Batal
                </button>
                <Button type="submit" disabled={busy}>
                  {busy && <Spinner />}
                  {busy ? "Memverifikasi…" : "Verifikasi"}
                </Button>
              </div>
            </fieldset>
          </form>
        )}
        {challenge && (
          <button
            type="button"
            className="text-link"
            disabled={busy}
            onClick={() => void resend()}
          >
            {busy ? "Mengirim ulang…" : "Kirim ulang kode"}
          </button>
        )}
      </div>
    </div>
  );
}