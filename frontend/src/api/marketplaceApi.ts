import { authApi } from "./authApi";
export type PricingType = "PerVisit" | "PerMonth";
export type VerificationStatus = "Pending" | "Verified" | "Rejected";
export const days = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
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
  orders: () => authApi.get<Order[]>("/api/orders"),
  book: (body: unknown) => authApi.post<Order>("/api/orders", body),
  review: (id: string, body: unknown) =>
    authApi.post(`/api/orders/${id}/review`, body),
};
export const adminApi = {
  roster: () => authApi.get<ProviderAdmin[]>("/api/admin/providers"),
  provider: (id: string) =>
    authApi.get<ProviderAdmin>(`/api/admin/providers/${id}`),
  draft: (agencyId: string | null) =>
    authApi.post<ProviderAdmin>("/api/admin/providers", { agencyId }),
  saveProvider: (id: string, step: string, body: unknown) =>
    authApi.post<ProviderAdmin>(`/api/admin/providers/${id}/${step}`, body),
  agencies: () => authApi.get<Agency[]>("/api/admin/agencies"),
  categories: () => authApi.get<Category[]>("/api/admin/categories"),
  orders: () => authApi.get<Order[]>("/api/admin/orders"),
};
