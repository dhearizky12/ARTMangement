import { House, LayoutGrid, UserRound } from "lucide-react";
import { NavLink } from "react-router-dom";
export function BottomNav() {
  return (
    <nav className="bottom-nav" aria-label="Navigasi utama">
      <NavLink end to="/dashboard">
        <House aria-hidden="true" />
        <span>Beranda</span>
      </NavLink>
      <NavLink to="/categories">
        <LayoutGrid aria-hidden="true" />
        <span>Layanan</span>
      </NavLink>
      <NavLink to="/account">
        <UserRound aria-hidden="true" />
        <span>Akun</span>
      </NavLink>
    </nav>
  );
}
