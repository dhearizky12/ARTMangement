import type { ReactNode } from "react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Brand } from "../Brand";
import { Button } from "../ui";
import { authApi } from "../../api/authApi";

export function ProviderLayout({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [error, setError] = useState("");
  async function logout() {
    try {
      await authApi.logout();
      navigate("/provider/login", { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal keluar.");
    }
  }
  return (
    <div className="app-shell">
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <div className="provider-header-actions">
            <span className="header-note">Ruang kerja Provider</span>
            <Button variant="ghost" onClick={logout}>Keluar</Button>
          </div>
        </div>
      </header>
      <main className="page provider-page">
        {error && <p className="error" role="alert">{error}</p>}
        {children}
      </main>
    </div>
  );
}
