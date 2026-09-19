import { House, Search, ClipboardList, UserRound } from "lucide-react";
import { NavLink } from "react-router-dom";
export function BottomNav() {
  return (
    <nav className="bottom-nav" aria-label="Navigasi utama">
      {[
        { to: "/", label: "Beranda", Icon: House },
        { to: "/search", label: "Cari", Icon: Search },
        { to: "/orders", label: "Pesanan", Icon: ClipboardList },
        { to: "/account", label: "Akun", Icon: UserRound },
      ].map(({ to, label, Icon }) => (
        <NavLink key={to} end={to === "/"} to={to}>
          <Icon aria-hidden="true" />
          <span>{label}</span>
        </NavLink>
      ))}
    </nav>
  );
}
