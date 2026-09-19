export type Role = "Customer" | "PlatformAdmin" | "AgencyAdmin";
export interface User {
  id: string;
  email: string;
  fullName: string;
  pictureUrl: string | null;
  role: Role;
  agencyId?: string | null;
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
    method?: string,
  ): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: method || (body === undefined ? "GET" : "POST"),
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
  publicGet<T>(path: string): Promise<T> {
    return this.request<T>(path);
  }
  async mutate<T>(
    path: string,
    method: "PUT" | "DELETE",
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
