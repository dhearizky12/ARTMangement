import type { ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { Brand } from "../Brand";
import { useAuth } from "../../context/AuthContext";
export function AdminLayout({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  return (
    <>
      <header className="site-header">
        <div className="header-inner">
          <Brand />
          <Link className="text-link" to="/account">
            Akun
          </Link>
        </div>
      </header>
      <main className="page admin-page">
        <p className="eyebrow">
          {session?.user.role === "AgencyAdmin"
            ? "Ruang kelola agency"
            : "Ruang kelola platform"}
        </p>
        <nav className="filter-chips" aria-label="Menu admin">
          <NavLink className="btn btn-ghost" end to="/admin">
            Penyedia
          </NavLink>
          <NavLink className="btn btn-ghost" to="/admin/orders">
            Pesanan
          </NavLink>
          {session?.user.role === "PlatformAdmin" && (
            <>
              <NavLink className="btn btn-ghost" to="/admin/agencies">
                Agency
              </NavLink>
              <NavLink className="btn btn-ghost" to="/admin/catalog">
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
