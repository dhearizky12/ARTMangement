import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { Brand } from "../Brand";
import { useAuth } from "../../context/AuthContext";
import { LogoutButton } from "../LogoutButton";
import { useAdminMeta } from "../../hooks/useAdminMeta";
export function AdminLayout({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  useAdminMeta();
  return (
    <>
      <header className="site-header">
        <div className="header-inner">
          <Brand to="/admin/dashboard" />
          <LogoutButton variant="text" redirectTo="/admin/login" />
        </div>
      </header>
      <main className="page admin-page">
        <p className="eyebrow">
          {session?.user.role === "AgencyAdmin"
            ? "Ruang kelola agency"
            : "Ruang kelola platform"}
        </p>
        <nav className="filter-chips" aria-label="Menu admin">
          <NavLink className="btn btn-ghost" to="/admin/dashboard">
            Dashboard
          </NavLink>
          <NavLink
            className="btn btn-ghost"
            to="/admin/dashboard#verification-queue"
          >
            Queue verifikasi
          </NavLink>
          <NavLink
            className="btn btn-ghost"
            to="/admin/dashboard#provider-roster"
          >
            Roster provider
          </NavLink>
          <NavLink className="btn btn-ghost" to="/admin/orders">
            Pesanan
          </NavLink>
          <NavLink className="btn btn-ghost" to="/admin/reviews">
            Ulasan
          </NavLink>
          {session?.user.role === "PlatformAdmin" && (
            <>
              <NavLink className="btn btn-ghost" to="/admin/agencies">
                Agency
              </NavLink>
              <NavLink className="btn btn-ghost" to="/admin/content">
                Kategori & konten
              </NavLink>
              <NavLink className="btn btn-ghost" to="/admin/audit">
                Audit
              </NavLink>
            </>
          )}
        </nav>
        {children}
      </main>
    </>
  );
}