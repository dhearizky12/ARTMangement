import type { ReactNode } from "react";
import { Brand } from "./Brand";
import { BottomNav } from "./BottomNav";
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="app-shell">
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <span className="header-note">Bantuan tepat. Hari lebih hebat.</span>
        </div>
      </header>
      <main className="page">{children}</main>
      <BottomNav />
    </div>
  );
}
