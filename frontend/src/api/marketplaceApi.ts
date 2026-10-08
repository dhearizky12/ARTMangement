import { authApi } from "./authApi";
export type PricingType = "PerVisit" | "PerMonth";
export type VerificationStatus = "Pending" | "Verified" | "Rejected";
export type ProviderApplicationStatus =
  | "Draft"
  | "Submitted"
  | "NeedsChanges"
  | "Approved"
  | "Rejected"
  | "Suspended";
export const days = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
] as const;
export const dayNames: Record<string, string> = {
  Sunday: "Minggu",
  Monday: "Senin",
  Tuesday: "Selasa",
  Wednesday: "Rabu",
  Thursday: "Kamis",
  Friday: "Jumat",
  Saturday: "Sabtu",
};
export interface Availability {
  dayOfWeek: string;
  isAvailable: boolean;
}
export interface Provider {
  id: string;
  agencyId: string | null;
  agencyName: string | null;
  fullName: string;
  age: number;
  bio: string;
  yearsOfExperience: number;
  jobsCompletedCount: number;
  pricingType: PricingType;
  price: number;
  verificationStatus: VerificationStatus;
  identityVerified: boolean;
  backgroundCheckPassed: boolean;
  contractSigned: boolean;
  location: string | null;
  categories: { id: string; name: string }[];
  skills: string[];
  languages: string[];
  availability: Availability[];
  rating: number | null;
  reviewCount: number;
  reviews: { rating: number; comment: string; createdAt: string }[];
  applicationStatus: ProviderApplicationStatus;
  moderationNote: string | null;
}
export interface ProviderAdmin {
  provider: Provider;
  step: "personal" | "address" | "documents" | "profile" | "verify";
  villageId: string | null;
  addressDetail: string;
  postalCode: string;
  documents: { id: string; documentType: string; uploadedAt: string }[];
}
export interface Agency {
  id: string;
  name: string;
  contactInfo: string;
  status: "Pending" | "Approved" | "Suspended";
}
export interface Category {
  id: string;
  slug: string;
  name: string;
  description: string;
  iconKey: string;
  sortOrder: number;
  isActive: boolean;
  isFeatured: boolean;
}
export interface Content {
  id: string;
  title: string;
  body: string;
  sortOrder: number;
  iconName: string | null;
  isActive: boolean;
}
export interface ReviewAdmin {
  id: string;
  orderId: string;
  providerId: string;
  providerName: string;
  customerId: string;
  reviewerName: string;
  rating: number;
  comment: string;
  createdAt: string;
  isHidden: boolean;
  hiddenReason: string | null;
  hiddenAt: string | null;
}
export interface Order {
  id: string;
  providerId: string;
  providerName: string;
  status: "Pending" | "Confirmed" | "Completed" | "Cancelled";
  scheduledDate: string;
  price: number;
  pricingType: PricingType;
  addressDetail: string;
  villageId: string;
  reviewed: boolean;
}
export interface ProviderPage {
  total: number;
  page: number;
  pageSize: number;
  items: Provider[];
}
export interface ApplicationSection {
  id: "personal" | "address" | "documents" | "profile";
  label: string;
  status: "belum" | "sebagian" | "lengkapi";
}
export interface ProviderApplication {
  provider: ProviderAdmin;
  status: ProviderApplicationStatus;
  step: ProviderAdmin["step"];
  note: string | null;
  submittedAt: string | null;
  canEdit: boolean;
  sections: ApplicationSection[];
}
export const money = (price: number) =>
  new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    maximumFractionDigits: 0,
  }).format(price);
export const priceUnit = (type: PricingType) =>
  type === "PerMonth" ? "bulan" : "kunjungan";
export const marketplaceApi = {
  providers: (params: URLSearchParams) =>
    authApi.publicGet<ProviderPage>(`/api/providers?${params}`),
  provider: (id: string) => authApi.publicGet<Provider>(`/api/providers/${id}`),
  content: () => authApi.publicGet<Content[]>("/api/content"),
  trustSections: () => authApi.publicGet<Content[]>("/api/trust-sections"),
  orders: () => authApi.get<Order[]>("/api/orders"),
  book: (body: unknown) => authApi.post<Order>("/api/orders", body),
  review: (id: string, body: unknown) =>
    authApi.post(`/api/orders/${id}/review`, body),
};
export const adminApi = {
  roster: () => authApi.get<ProviderAdmin[]>("/api/admin/providers"),
  provider: (id: string) =>
    authApi.get<ProviderAdmin>(`/api/admin/providers/${id}`),
  draft: (agencyId: string | null, email: string, password: string) =>
    authApi.post<ProviderAdmin>("/api/admin/providers", {
      agencyId,
      email,
      password,
    }),
  saveProvider: (id: string, step: string, body: unknown) =>
    authApi.post<ProviderAdmin>(`/api/admin/providers/${id}/${step}`, body),
  agencies: () => authApi.get<Agency[]>("/api/admin/agencies"),
  categories: () => authApi.get<Category[]>("/api/admin/categories"),
  orders: () => authApi.get<Order[]>("/api/admin/orders"),
  applications: (status = "Submitted") =>
    authApi.get<ProviderAdmin[]>(`/api/admin/providers/applications?status=${status}`),
  verificationQueue: () =>
    authApi.get<ProviderAdmin[]>("/api/admin/providers/verification-queue"),
  verify: (id: string, body: unknown) =>
    authApi.post<ProviderAdmin>(`/api/admin/providers/${id}/verify`, body),
  moderate: (id: string, action: "approve" | "reject" | "request-changes" | "suspend", note?: string) =>
    authApi.post<ProviderAdmin>(`/api/admin/providers/${id}/${action}`, { note }),
  suspend: (id: string, note: string) =>
    authApi.mutate<ProviderAdmin>(`/api/admin/providers/${id}/suspend`, "PATCH", { note }),
  reactivate: (id: string) =>
    authApi.mutate<ProviderAdmin>(`/api/admin/providers/${id}/reactivate`, "PATCH"),
  suspendAgency: (id: string) =>
    authApi.mutate<Agency>(`/api/admin/agencies/${id}/suspend`, "PATCH"),
  reactivateAgency: (id: string) =>
    authApi.mutate<Agency>(`/api/admin/agencies/${id}/reactivate`, "PATCH"),
  reviews: () => authApi.get<ReviewAdmin[]>("/api/admin/reviews"),
  hideReview: (id: string, reason: string) =>
    authApi.mutate<ReviewAdmin>(`/api/admin/reviews/${id}/hide`, "PATCH", { reason }),
  restoreReview: (id: string) =>
    authApi.mutate<ReviewAdmin>(`/api/admin/reviews/${id}/restore`, "PATCH"),
  trustSections: () => authApi.get<Content[]>("/api/admin/trust-sections"),
  createTrustSection: (body: unknown) =>
    authApi.post<Content>("/api/admin/trust-sections", body),
  updateTrustSection: (id: string, body: unknown) =>
    authApi.mutate<Content>(`/api/admin/trust-sections/${id}`, "PUT", body),
  deleteTrustSection: (id: string) =>
    authApi.mutate<void>(`/api/admin/trust-sections/${id}`, "DELETE"),
};
export const providerApi = {
  profile: () => authApi.get<Provider>("/api/provider/profile"),
  application: () => authApi.get<ProviderApplication>("/api/provider/application/status"),
  submit: () => authApi.post<ProviderApplication>("/api/provider/application/submit", {}),
  personal: (body: unknown) => authApi.post<ProviderAdmin>("/api/provider/personal-info", body),
  address: (body: unknown) => authApi.post<ProviderAdmin>("/api/provider/address", body),
  profileDetails: (body: unknown) => authApi.post<ProviderAdmin>("/api/provider/profile", body),
  documents: (body: FormData) => authApi.post<ProviderAdmin>("/api/provider/documents", body),
  downloadDocument: (id: string) => authApi.download(`/api/provider/documents/${id}`),
  availability: (availability: Availability[]) =>
    authApi.mutate<Provider>("/api/provider/availability", "PUT", {
      availability,
    }),
  orders: () => authApi.get<Order[]>("/api/provider/orders"),
};
