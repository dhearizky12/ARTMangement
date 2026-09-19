import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthApi } from "./authApi";
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
      .mockResolvedValueOnce(new Response(JSON.stringify(session), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(session), { status: 200 }));
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
      vi.fn()
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
});
