import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthApi, sessionLevel } from "./authApi";
const session = {
  accessToken: "test-token",
  expiresAt: new Date(Date.now() + 600000).toISOString(),
  refreshToken: "refresh-token",
  refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
  user: { role: "Customer" },
};
afterEach(() => vi.unstubAllGlobals());
describe("AuthApi", () => {
  it("shares one refresh request across concurrent consumers", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify(session), { status: 200 }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify(session), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    const [a, b] = await Promise.all([api.refresh(), api.refresh()]);
    expect(a).toEqual(b);
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[1][1].credentials).toBeUndefined();
    expect(JSON.parse(fetch.mock.calls[1][1].body)).toEqual({
      refreshToken: "refresh-token",
    });
  });
  it("retries a protected request with a refreshed token on 401", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(session)))
      .mockResolvedValueOnce(new Response("{}", { status: 401 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ ...session, accessToken: "new-token" })),
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ message: "ok" })));
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    expect(await api.get("/api/admin/dashboard")).toEqual({ message: "ok" });
    expect(fetch.mock.calls[3][1].headers.Authorization).toBe(
      "Bearer new-token",
    );
  });
  it("clears the in-memory session after refresh rejection", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(JSON.stringify(session)))
        .mockResolvedValueOnce(new Response("{}", { status: 401 })),
    );
    const api = new AuthApi("https://api.example.test");
    api.onSession = vi.fn();
    await api.admin("admin@example.test", "password");
    await expect(api.refresh()).rejects.toMatchObject({ status: 401 });
    expect(api.onSession).toHaveBeenCalledWith(null);
  });
  it("sends multipart uploads with a bearer token and lets fetch create the boundary", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(session)))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ profileStep: "done" })),
      );
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    const body = new FormData();
    body.append("documentType", "KTP");
    body.append("file", new Blob(["test"], { type: "image/png" }), "test.png");
    expect(
      await api.post("/api/admin/providers/test-id/documents", body),
    ).toEqual({
      profileStep: "done",
    });
    expect(fetch.mock.calls[1][1].headers).not.toHaveProperty("Content-Type");
    expect(fetch.mock.calls[1][1].headers.Authorization).toBe(
      "Bearer test-token",
    );
    expect(fetch.mock.calls[1][1].body).toBe(body);
  });
  it("loads the public catalog without requesting a login or refresh", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify([])));
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    expect(await api.publicGet("/api/providers")).toEqual([]);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch.mock.calls[0][0]).toBe(
      "https://api.example.test/api/providers",
    );
    expect(fetch.mock.calls[0][1].headers).not.toHaveProperty("Authorization");
  });
  it("returns a 2FA challenge without creating a session", async () => {
    const challenge = {
      requiresTwoFactor: true,
      challengeId: "challenge-1",
      method: "email",
      maskedEmail: "a***@example.test",
      fallbackAllowed: true,
    };
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(challenge)));
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    const result = await api.admin("admin@example.test", "password");
    expect(result).toEqual(challenge);
    expect(api.currentSession).toBeNull();
    expect(fetch.mock.calls[0][0]).toBe(
      "https://api.example.test/api/auth/admin/login",
    );
  });
  it("stores the session returned by the 2FA verify step", async () => {
    const verified = { ...session, accessToken: "mfa-token" };
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(verified)));
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    const result = await api.verifyTwoFactor("challenge-1", "123456");
    expect(result.accessToken).toBe("mfa-token");
    expect(api.currentSession?.accessToken).toBe("mfa-token");
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
      challengeId: "challenge-1",
      code: "123456",
    });
  });
  it("dispatches a step-up-required event on an mfa_required error", async () => {
    const listeners: Record<string, () => void> = {};
    vi.stubGlobal("window", {
      addEventListener: (type: string, fn: () => void) => {
        listeners[type] = fn;
      },
      dispatchEvent: (e: Event) => {
        listeners[e.type]?.();
        return true;
      },
      localStorage: memoryStorage(),
    });
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(session)))
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            title: "Verifikasi email diperlukan.",
            status: 403,
            code: "mfa_required",
          }),
          { status: 403 },
        ),
      );
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    const handler = vi.fn();
    window.addEventListener("step-up-required", handler);
    await expect(api.get("/api/provider/application")).rejects.toMatchObject({
      code: "mfa_required",
    });
    expect(handler).toHaveBeenCalledTimes(1);
  });
});
describe("sessionLevel", () => {
  it("maps the amr claim to a full level", () => {
    expect(sessionLevel(token({ amr: "email_otp" }))).toBe("full");
  });
  it("maps the limited claim to a limited level", () => {
    expect(sessionLevel(token({ limited: "true" }))).toBe("limited");
  });
  it("defaults to none for tokens without 2FA claims", () => {
    expect(sessionLevel(token({ sub: "1" }))).toBe("none");
  });
  it("falls back to none on malformed tokens", () => {
    expect(sessionLevel("not-a-token")).toBe("none");
  });
});
function token(payload: object) {
  return `x.${btoa(JSON.stringify(payload))
    .replace(/=+$/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")}.y`;
}
function memoryStorage(): Storage {
  const store = new Map<string, string>();
  return {
    getItem: (key) => (store.has(key) ? store.get(key)! : null),
    setItem: (key, value) => void store.set(key, String(value)),
    removeItem: (key) => void store.delete(key),
    clear: () => store.clear(),
    key: (index) => [...store.keys()][index] ?? null,
    get length() {
      return store.size;
    },
  };
}
const SESSION_KEY = "bantubantu.session";
describe("AuthApi session persistence", () => {
  it("persists a login and restores the session in a fresh instance", async () => {
    const storage = memoryStorage();
    vi.stubGlobal("window", { localStorage: storage });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(session))),
    );
    const first = new AuthApi("https://api.example.test");
    await first.admin("admin@example.test", "password");
    expect(storage.getItem(SESSION_KEY)).toContain("refresh-token");
    const second = new AuthApi("https://api.example.test");
    expect(second.currentSession?.refreshToken).toBe("refresh-token");
    expect(second.currentSession?.user.role).toBe("Customer");
  });
  it("drops a stored session whose refresh token has expired", () => {
    const storage = memoryStorage();
    storage.setItem(
      SESSION_KEY,
      JSON.stringify({
        ...session,
        refreshExpiresAt: new Date(Date.now() - 1000).toISOString(),
      }),
    );
    vi.stubGlobal("window", { localStorage: storage });
    const api = new AuthApi("https://api.example.test");
    expect(api.currentSession).toBeNull();
    expect(storage.getItem(SESSION_KEY)).toBeNull();
  });
  it("ignores malformed stored data", () => {
    const storage = memoryStorage();
    storage.setItem(SESSION_KEY, "not-json{");
    vi.stubGlobal("window", { localStorage: storage });
    const api = new AuthApi("https://api.example.test");
    expect(api.currentSession).toBeNull();
    expect(storage.getItem(SESSION_KEY)).toBeNull();
  });
  it("clears the stored session when refresh is rejected", async () => {
    const storage = memoryStorage();
    vi.stubGlobal("window", { localStorage: storage });
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(JSON.stringify(session)))
        .mockResolvedValueOnce(new Response("{}", { status: 401 })),
    );
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    expect(storage.getItem(SESSION_KEY)).not.toBeNull();
    await expect(api.refresh()).rejects.toMatchObject({ status: 401 });
    expect(storage.getItem(SESSION_KEY)).toBeNull();
  });
  it("clears the stored session on logout", async () => {
    const storage = memoryStorage();
    vi.stubGlobal("window", { localStorage: storage });
    vi.stubGlobal("sessionStorage", memoryStorage());
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(new Response(JSON.stringify(session)))
        .mockResolvedValueOnce(new Response(null, { status: 204 })),
    );
    const api = new AuthApi("https://api.example.test");
    await api.admin("admin@example.test", "password");
    expect(storage.getItem(SESSION_KEY)).not.toBeNull();
    await api.logout();
    expect(storage.getItem(SESSION_KEY)).toBeNull();
    expect(api.currentSession).toBeNull();
  });
});
