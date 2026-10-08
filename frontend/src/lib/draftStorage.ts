export interface PersonalDraft {
  fullName: string;
  age: string;
  bio: string;
  experience: string;
}
export interface AddressDraft {
  addressDetail: string;
  postalCode: string;
  villageId: string;
  villageLabel: string;
}
export interface DocumentsDraft {
  documentType: string;
  fileName: string;
  file?: File;
}
export interface ProfileDraft {
  categoryIds: string[];
  skills: string;
  languages: string;
  pricingType: string;
  price: string;
  days: string[];
}
export interface Drafts {
  personal?: PersonalDraft;
  address?: AddressDraft;
  documents?: DocumentsDraft;
  profile?: ProfileDraft;
}
export type SectionId = keyof Drafts;

const PREFIX = "bantubantu:onboarding:";

export function draftKey(providerId: string): string {
  return PREFIX + providerId;
}

function withoutFile<T extends object>(draft: T): T {
  if (draft && typeof draft === "object" && "file" in draft) {
    const rest = { ...draft } as T & { file?: unknown };
    delete rest.file;
    return rest;
  }
  return draft;
}

export function loadDrafts(providerId: string): Drafts {
  try {
    const raw = sessionStorage.getItem(draftKey(providerId));
    if (!raw) return {};
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object") return {};
    const out: Record<string, unknown> = {};
    for (const id of ["personal", "address", "documents", "profile"]) {
      const value = (parsed as Record<string, unknown>)[id];
      if (value && typeof value === "object") out[id] = value;
    }
    return out as Drafts;
  } catch {
    return {};
  }
}

export function saveDrafts(providerId: string, drafts: Drafts): void {
  try {
    const clean: Record<string, unknown> = {};
    for (const id of ["personal", "address", "documents", "profile"]) {
      const draft = drafts[id as SectionId];
      if (draft) clean[id] = withoutFile(draft);
    }
    const key = draftKey(providerId);
    if (Object.keys(clean).length)
      sessionStorage.setItem(key, JSON.stringify(clean));
    else sessionStorage.removeItem(key);
  } catch {
    return;
  }
}

export function clearAllOnboardingDrafts(): void {
  try {
    for (let i = sessionStorage.length - 1; i >= 0; i--) {
      const key = sessionStorage.key(i);
      if (key && key.startsWith(PREFIX)) sessionStorage.removeItem(key);
    }
  } catch {
    return;
  }
}
