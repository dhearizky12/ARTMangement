import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { LogOut } from "lucide-react";
import { Brand } from "../Brand";
import { Button, ConfirmDialog } from "../ui";
import {
  authApi,
  sessionLevel,
  type Session,
} from "../../api/authApi";
import { useAuth } from "../../context/AuthContext";
import { StepUpDialog } from "./StepUpDialog";

export function ProviderLayout({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const { session } = useAuth();
  const sessionRef = useRef<Session | null>(session);
  useEffect(() => {
    sessionRef.current = session;
  }, [session]);
  const [error, setError] = useState("");
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [stepUpOpen, setStepUpOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const onStepUpRequired = () => {
      const active = sessionRef.current;
      if (active && sessionLevel(active.accessToken) === "limited")
        setStepUpOpen(true);
    };
    window.addEventListener("step-up-required", onStepUpRequired);
    return () => window.removeEventListener("step-up-required", onStepUpRequired);
  }, []);
  const level = session ? sessionLevel(session.accessToken) : "none";
  async function logout() {
    setBusy(true);
    setError("");
    try {
      await authApi.logout();
      navigate("/provider/login", { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal keluar.");
      setConfirmLogout(false);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="app-shell">
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <div className="provider-header-actions">
            <span className="header-note">Ruang kerja Provider</span>
            <Button
              variant="ghost"
              className="icon-btn"
              aria-label="Keluar"
              title="Keluar"
              onClick={() => setConfirmLogout(true)}
            >
              <LogOut size={18} aria-hidden="true" />
            </Button>
          </div>
        </div>
      </header>
      <main className="page provider-page">
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        {level === "limited" && (
          <div className="step-up-banner" role="note">
            <div>
              <strong>Sesi terbatas</strong>
              <p>
                Untuk mengirim aplikasi, memperbarui profil, atau mengganti kata
                sandi, verifikasi email Anda sekali selagi masuk.
              </p>
            </div>
            <Button onClick={() => setStepUpOpen(true)}>Verifikasi email</Button>
          </div>
        )}
        {children}
      </main>
      <StepUpDialog open={stepUpOpen} onOpenChange={setStepUpOpen} />
      <ConfirmDialog
        open={confirmLogout}
        title="Keluar dari akun?"
        message="Sesi Anda akan diakhiri. Anda perlu masuk kembali untuk mengelola layanan."
        confirmLabel="Keluar"
        busy={busy}
        onConfirm={() => void logout()}
        onCancel={() => setConfirmLogout(false)}
      />
    </div>
  );
}
