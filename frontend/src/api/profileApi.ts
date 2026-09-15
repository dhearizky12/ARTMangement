import { authApi, type User } from "./authApi";
export interface PersonalInfo {
  fullName: string;
  birthDate: string;
  phoneNumber: string;
  gender: string;
}
export interface Address {
  villageId: string | null;
  villageName: string | null;
  province: string;
  city: string;
  district: string;
  addressLine: string;
  postalCode: string;
}
export interface ProfileStatus {
  profileStep: User["profileStep"];
  profileCompleted: boolean;
  personalCompleted: boolean;
  addressCompleted: boolean;
  documentsCompleted: boolean;
  personalInfo: PersonalInfo | null;
  address: Address | null;
  document: {
    documentType: string;
    verificationStatus: string;
    uploadedAt: string;
  } | null;
  documentRules: {
    maxBytes: number;
    contentTypes: string[];
    documentTypes: string[];
  };
}
export const profileApi = {
  status: () => authApi.get<ProfileStatus>("/api/profile/status"),
  personal: (body: PersonalInfo) =>
    authApi.post<ProfileStatus>("/api/profile/personal-info", body),
  address: (body: {
    villageId: string;
    addressDetail: string;
    postalCode: string;
  }) => authApi.post<ProfileStatus>("/api/profile/address", body),
  documents: (body: FormData) =>
    authApi.post<ProfileStatus>("/api/profile/documents", body),
};
export const profileRoutes: Record<User["profileStep"], string> = {
  personal: "/onboarding/personal-info",
  address: "/onboarding/address",
  documents: "/onboarding/documents",
  done: "/dashboard",
};
