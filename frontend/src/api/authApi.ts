export type Role = "Customer" | "PlatformAdmin" | "AgencyAdmin" | "Provider";
export interface User {
  id: string;
  email: string;
  fullName: string;
  pictureUrl: string | null;
  role: Role;
  agencyId?: string | null;
  profileCompleted: boolean;
  profileStep: "personal" | "address" | "documents" | "done";
  applicationStatus?:
    | "Draft"
    | "Submitted"
    | "NeedsChanges"
    | "Approved"
    | "Rejected"
    | "Suspended";
}
export interface Session {
  accessToken: string;
  expiresAt: string;
  user: User;
  refreshToken: string;
  refreshExpiresAt: string;
}
export interface TwoFactorLoginChallenge {
  requiresTwoFactor: true;
  challengeId: string;
  method: "email";
  maskedEmail: string;
  fallbackAllowed: boolean;
}
export type SessionLevel = "full" | "limited" | "none";
export function sessionLevel(accessToken: string): SessionLevel {
  try {
    const payload = accessToken.split(".")[1];
    if (!payload) return "none";
    const json = JSON.parse(
      atob(payload.replace(/-/g, "+").replace(/_/g, "/")),
    );
    if (json.amr === "email_otp") return "full";
    if (json.limited === "true") return "limited";
    return "none";
  } catch {
    return "none";
  }
}
import { clearAllOnboardingDrafts } from "../lib/draftStorage";
const SESSION_STORAGE_KEY = "bantubantu.session";
function loadStoredSession(): Session | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Session;
    if (!parsed?.refreshToken || !parsed.refreshExpiresAt) return null;
    if (Date.parse(parsed.refreshExpiresAt) <= Date.now()) {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }
    return parsed;
  } catch {
    try {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      /* ignore unreadable storage */
    }
    return null;
  }
}
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public code?: string,
  ) {
    super(message);
  }
}
export class AuthApi {
  private session: Session | null = null;
  private refreshPending: Promise<Session> | null = null;
  onSession: (session: Session | null) => void = () => {};
  constructor(private baseUrl: string) {
    this.session = loadStoredSession();
  }
  get currentSession(): Session | null {
    return this.session;
  }
  private setSession(session: Session | null) {
    this.session = session;
    this.storeSession(session);
    this.onSession(session);
    return session;
  }
  private storeSession(session: Session | null) {
    if (typeof window === "undefined") return;
    try {
      if (session)
        window.localStorage.setItem(
          SESSION_STORAGE_KEY,
          JSON.stringify(session),
        );
      else window.localStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      /* storage unavailable or full: keep the in-memory session only */
    }
  }
  private async request<T>(
    path: string,
    body?: unknown,
    token?: string,
    method?: string,
  ): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: method || (body === undefined ? "GET" : "POST"),
      headers: {
        ...(body === undefined || body instanceof FormData
          ? {}
          : { "Content-Type": "application/json" }),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      ...(body === undefined
        ? {}
        : { body: body instanceof FormData ? body : JSON.stringify(body) }),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      if (error.code === "PROFILE_INCOMPLETE" && typeof window !== "undefined")
        window.dispatchEvent(new Event("profile-required"));
      if (error.code === "mfa_required" && typeof window !== "undefined")
        window.dispatchEvent(new Event("step-up-required"));
      const details = error.errors
        ? Object.values(error.errors).flat().join(" ")
        : null;
      throw new ApiError(
        response.status,
        details || error.title || "Permintaan gagal. Silakan coba lagi.",
        error.code,
      );
    }
    return response.status === 204 ? (undefined as T) : response.json();
  }
  async google(credential: string) {
    const s = await this.request<Session>("/api/auth/google", { credential });
    this.setSession(s);
  }
  async admin(email: string, password: string) {
    return this.loginOrChallenge("/api/auth/admin/login", { email, password });
  }
  async provider(email: string, password: string) {
    return this.loginOrChallenge("/api/auth/provider/login", {
      email,
      password,
    });
  }
  private async loginOrChallenge(
    path: string,
    body: unknown,
  ): Promise<Session | TwoFactorLoginChallenge> {
    const data = await this.request<Session | TwoFactorLoginChallenge>(
      path,
      body,
    );
    if ("accessToken" in data) {
      this.setSession(data as Session);
      return data as Session;
    }
    return data as TwoFactorLoginChallenge;
  }
  async verifyTwoFactor(challengeId: string, code: string): Promise<Session> {
    const s = await this.request<Session>("/api/auth/2fa/verify", {
      challengeId,
      code,
    });
    this.setSession(s);
    return s;
  }
  async resendTwoFactor(challengeId: string) {
    await this.request<void>("/api/auth/2fa/resend", { challengeId });
  }
  async skipTwoFactor(challengeId: string): Promise<Session> {
    const s = await this.request<Session>("/api/auth/2fa/skip", {
      challengeId,
    });
    this.setSession(s);
    return s;
  }
  async startProviderStepUp(): Promise<TwoFactorLoginChallenge> {
    return this.post<TwoFactorLoginChallenge>("/api/provider/step-up/start", {});
  }
  async verifyProviderStepUp(
    challengeId: string,
    code: string,
  ): Promise<Session> {
    const s = await this.post<Session>("/api/provider/step-up/verify", {
      challengeId,
      code,
      refreshToken: this.session?.refreshToken,
    });
    this.setSession(s);
    return s;
  }
  async registerProvider(
    email: string,
    password: string,
    confirmPassword: string,
  ) {
    const s = await this.request<Session>("/api/auth/provider/register", {
      email,
      password,
      confirmPassword,
    });
    this.setSession(s);
  }
  async changeProviderPassword(currentPassword: string, newPassword: string) {
    await this.post<void>("/api/auth/provider/change-password", {
      currentPassword,
      newPassword,
    });
  }
  refresh(): Promise<Session> {
    const refreshToken = this.session?.refreshToken;
    if (!refreshToken) {
      const error = new ApiError(401, "Sesi login tidak tersedia.");
      this.setSession(null);
      return Promise.reject(error);
    }
    if (!this.refreshPending)
      this.refreshPending = this.request<Session>("/api/auth/refresh", {
        refreshToken,
      })
        .then((s) => {
          this.setSession(s);
          return s;
        })
        .catch((e) => {
          this.setSession(null);
          throw e;
        })
        .finally(() => {
          this.refreshPending = null;
        });
    return this.refreshPending;
  }
  async logout() {
    if (this.session?.refreshToken)
      await this.request("/api/auth/logout", {
        refreshToken: this.session.refreshToken,
      });
    this.setSession(null);
    clearAllOnboardingDrafts();
  }
  updateUser(user: Partial<User>) {
    if (this.session)
      this.setSession({
        ...this.session,
        user: { ...this.session.user, ...user },
      });
  }
  publicGet<T>(path: string): Promise<T> {
    return this.request<T>(path);
  }
  async mutate<T>(
    path: string,
    method: "PUT" | "PATCH" | "DELETE",
    body?: unknown,
  ): Promise<T> {
    return this.authorized<T>(path, body, method);
  }
  async download(path: string): Promise<Blob> {
    const session =
      this.session && Date.parse(this.session.expiresAt) > Date.now() + 30000
        ? this.session
        : await this.refresh();
    const response = await fetch(`${this.baseUrl}${path}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
    });
    if (!response.ok)
      throw new ApiError(response.status, "Dokumen gagal diunduh.");
    return response.blob();
  }
  async get<T>(path: string): Promise<T> {
    return this.authorized<T>(path);
  }
  async post<T>(path: string, body: unknown): Promise<T> {
    return this.authorized<T>(path, body);
  }
  private async authorized<T>(
    path: string,
    body?: unknown,
    method?: string,
  ): Promise<T> {
    let session = this.session;
    if (!session || Date.parse(session.expiresAt) <= Date.now() + 30000)
      session = await this.refresh();
    try {
      return await this.request<T>(path, body, session.accessToken, method);
    } catch (e) {
      if (!(e instanceof ApiError) || e.status !== 401) throw e;
      const updated = await this.refresh();
      return this.request<T>(path, body, updated.accessToken, method);
    }
  }
}
export const authApi = new AuthApi(
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "",
);
