import type { ReactNode } from "react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { LogOut } from "lucide-react";
import { Brand } from "../Brand";
import { Button, ConfirmDialog } from "../ui";
import { authApi } from "../../api/authApi";

export function ProviderLayout({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [error, setError] = useState("");
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [busy, setBusy] = useState(false);
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
        {children}
      </main>
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
