import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthApi } from "./authApi";
const session = {
  accessToken: "test-token",
  expiresAt: new Date(Date.now() + 600000).toISOString(),
  user: { role: "User" },
};
afterEach(() => vi.unstubAllGlobals());
describe("AuthApi", () => {
  it("shares one refresh request across concurrent consumers", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify(session), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    const [a, b] = await Promise.all([api.refresh(), api.refresh()]);
    expect(a).toEqual(b);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch.mock.calls[0][1].credentials).toBe("include");
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
      vi.fn().mockResolvedValue(new Response("{}", { status: 401 })),
    );
    const api = new AuthApi("https://api.example.test");
    api.onSession = vi.fn();
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
    await api.refresh();
    const body = new FormData();
    body.append("documentType", "KTP");
    body.append("file", new Blob(["test"], { type: "image/png" }), "test.png");
    expect(await api.post("/api/profile/documents", body)).toEqual({
      profileStep: "done",
    });
    expect(fetch.mock.calls[1][1].headers).not.toHaveProperty("Content-Type");
    expect(fetch.mock.calls[1][1].headers.Authorization).toBe(
      "Bearer test-token",
    );
    expect(fetch.mock.calls[1][1].body).toBe(body);
  });
  it("preserves the server profile guard code without retrying a forbidden request", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(session)))
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            title: "Lengkapi profil",
            code: "PROFILE_INCOMPLETE",
          }),
          { status: 403 },
        ),
      );
    vi.stubGlobal("fetch", fetch);
    const api = new AuthApi("https://api.example.test");
    await api.refresh();
    await expect(api.get("/api/service-categories")).rejects.toMatchObject({
      status: 403,
      code: "PROFILE_INCOMPLETE",
    });
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
