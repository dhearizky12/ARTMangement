import type { Role } from "../api/authApi";

export const ADMIN_ROLES: readonly Role[] = ["PlatformAdmin", "AgencyAdmin"];

export function isAdminRole(role: Role | undefined): boolean {
  return role === "PlatformAdmin" || role === "AgencyAdmin";
}

export function sanitizeAdminReturnTo(
  requested: string | null | undefined,
): string | null {
  if (!requested) return null;
  if (!requested.startsWith("/admin/")) return null;
  if (requested.startsWith("/admin//")) return null;
  if (requested.includes("\\") || requested.includes("..")) return null;
  return requested;
}

export function adminLoginReturn(
  returnToParam: string | null | undefined,
  role: Role | undefined,
): string {
  if (!isAdminRole(role)) return "/admin/dashboard";
  return sanitizeAdminReturnTo(returnToParam) ?? "/admin/dashboard";
}

export function adminEntryRedirect(pathname: string, search: string): string {
  return `/admin/login?returnTo=${encodeURIComponent(`${pathname}${search}`)}`;
}