export type Role = "User" | "Admin";
export interface User {
  id: string;
  email: string;
  fullName: string;
  pictureUrl: string | null;
  role: Role;
  profileCompleted: boolean;
  profileStep: "personal" | "address" | "documents" | "done";
}
export interface Session {
  accessToken: string;
  expiresAt: string;
  user: User;
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
  constructor(private baseUrl: string) {}
  private setSession(session: Session | null) {
    this.session = session;
    this.onSession(session);
    return session;
  }
  private async request<T>(
    path: string,
    body?: unknown,
    token?: string,
  ): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: body === undefined ? "GET" : "POST",
      credentials: "include",
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
    const s = await this.request<Session>("/api/auth/admin/login", {
      email,
      password,
    });
    this.setSession(s);
  }
  refresh(): Promise<Session> {
    if (!this.refreshPending)
      this.refreshPending = this.request<Session>("/api/auth/refresh", {})
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
    await this.request("/api/auth/logout", {});
    this.setSession(null);
  }
  updateUser(user: Partial<User>) {
    if (this.session)
      this.setSession({
        ...this.session,
        user: { ...this.session.user, ...user },
      });
  }
  async get<T>(path: string): Promise<T> {
    return this.authorized<T>(path);
  }
  async post<T>(path: string, body: unknown): Promise<T> {
    return this.authorized<T>(path, body);
  }
  private async authorized<T>(path: string, body?: unknown): Promise<T> {
    let session = this.session;
    if (!session || Date.parse(session.expiresAt) <= Date.now() + 30000)
      session = await this.refresh();
    try {
      return await this.request<T>(path, body, session.accessToken);
    } catch (e) {
      if (!(e instanceof ApiError) || e.status !== 401) throw e;
      const updated = await this.refresh();
      return this.request<T>(path, body, updated.accessToken);
    }
  }
}
export const authApi = new AuthApi(
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "",
);
