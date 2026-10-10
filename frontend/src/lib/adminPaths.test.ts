import { describe, expect, it } from "vitest";
import {
  adminEntryRedirect,
  adminLoginReturn,
  isAdminRole,
  sanitizeAdminReturnTo,
} from "./adminPaths";

describe("sanitizeAdminReturnTo", () => {
  it("accepts /admin/ paths", () => {
    expect(sanitizeAdminReturnTo("/admin/dashboard")).toBe("/admin/dashboard");
    expect(sanitizeAdminReturnTo("/admin/providers/123")).toBe(
      "/admin/providers/123",
    );
    expect(sanitizeAdminReturnTo("/admin/login")).toBe("/admin/login");
    expect(sanitizeAdminReturnTo("/admin/orders?status=Pending")).toBe(
      "/admin/orders?status=Pending",
    );
  });
  it("rejects null and empty input", () => {
    expect(sanitizeAdminReturnTo(null)).toBeNull();
    expect(sanitizeAdminReturnTo(undefined)).toBeNull();
    expect(sanitizeAdminReturnTo("")).toBeNull();
  });
  it("rejects absolute and protocol-relative URLs", () => {
    for (const path of [
      "https://evil.example",
      "http://evil.example",
      "//evil.example",
      "///evil.example",
    ])
      expect(sanitizeAdminReturnTo(path)).toBeNull();
  });
  it("rejects javascript: URLs", () => {
    for (const path of [
      "javascript:alert(1)",
      "JAVASCRIPT:alert(1)",
      "javascript://alert(1)",
    ])
      expect(sanitizeAdminReturnTo(path)).toBeNull();
  });
  it("rejects non-admin and auth paths", () => {
    for (const path of [
      "/",
      "/search",
      "/providers/1",
      "/login",
      "/provider/login",
      "/account",
    ])
      expect(sanitizeAdminReturnTo(path)).toBeNull();
  });
  it("rejects path traversal, backslashes and double slashes", () => {
    expect(sanitizeAdminReturnTo("/admin/../search")).toBeNull();
    expect(sanitizeAdminReturnTo("/admin/..")).toBeNull();
    expect(sanitizeAdminReturnTo("/admin\\search")).toBeNull();
    expect(sanitizeAdminReturnTo("/admin//evil")).toBeNull();
  });
});

describe("isAdminRole", () => {
  it("recognises both admin roles", () => {
    expect(isAdminRole("PlatformAdmin")).toBe(true);
    expect(isAdminRole("AgencyAdmin")).toBe(true);
  });
  it("rejects other roles", () => {
    expect(isAdminRole("Customer")).toBe(false);
    expect(isAdminRole("Provider")).toBe(false);
    expect(isAdminRole(undefined)).toBe(false);
  });
});

describe("adminLoginReturn", () => {
  it("always lands an admin on a valid admin path", () => {
    expect(adminLoginReturn("/admin/orders", "PlatformAdmin")).toBe(
      "/admin/orders",
    );
    expect(adminLoginReturn("/admin/dashboard?x=1", "AgencyAdmin")).toBe(
      "/admin/dashboard?x=1",
    );
  });
  it("falls back to /admin/dashboard for invalid returnTo", () => {
    for (const path of [null, "", "https://evil.example", "/search", "/admin//x"])
      expect(adminLoginReturn(path, "PlatformAdmin")).toBe("/admin/dashboard");
  });
  it("never uses returnTo for non-admin roles", () => {
    expect(adminLoginReturn("/admin/orders", "Customer")).toBe(
      "/admin/dashboard",
    );
    expect(adminLoginReturn("/admin/orders", "Provider")).toBe(
      "/admin/dashboard",
    );
    expect(adminLoginReturn("/admin/orders", undefined)).toBe(
      "/admin/dashboard",
    );
  });
});

describe("adminEntryRedirect", () => {
  it("preserves pathname and search", () => {
    expect(adminEntryRedirect("/admin/orders", "")).toBe(
      "/admin/login?returnTo=%2Fadmin%2Forders",
    );
    expect(adminEntryRedirect("/admin", "?tab=x")).toBe(
      "/admin/login?returnTo=%2Fadmin%3Ftab%3Dx",
    );
  });
});