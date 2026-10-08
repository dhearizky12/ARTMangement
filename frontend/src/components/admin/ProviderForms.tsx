import { useRef, useState, type FormEvent } from "react";
import { Button, Input, Select, Textarea } from "../ui";
import { AddressCombobox } from "../AddressCombobox";
import { ResourceState } from "../ResourceState";
import { useCategories } from "../../hooks/useCategories";
import {
  adminApi,
  days,
  dayNames,
  type ProviderAdmin,
} from "../../api/marketplaceApi";
import { authApi } from "../../api/authApi";
import type { VillageResult } from "../../api/wilayahApi";
import type {
  AddressDraft,
  DocumentsDraft,
  Drafts,
  PersonalDraft,
  ProfileDraft,
  SectionId,
} from "../../lib/draftStorage";
export interface FormProps {
  data: ProviderAdmin;
  save: (step: string, body: unknown) => Promise<void>;
  busy: boolean;
  downloadDocument?: (id: string) => Promise<Blob>;
  draft?: Drafts[SectionId];
  onDraftChange?: (id: SectionId, draft: Drafts[SectionId]) => void;
}

function str(v: FormDataEntryValue | null): string {
  return typeof v === "string" ? v : "";
}
function normList(v: FormDataEntryValue | null): string {
  return String(v ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean)
    .join(", ");
}
function normNum(v: FormDataEntryValue | number | null): string {
  return String(Number(v ?? 0));
}
function equal(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

export function personalFromForm(form: FormData): PersonalDraft {
  return {
    fullName: str(form.get("fullName")),
    age: str(form.get("age")),
    bio: str(form.get("bio")),
    experience: str(form.get("experience")),
  };
}
export function pristinePersonal(data: ProviderAdmin): PersonalDraft {
  const p = data.provider;
  return {
    fullName: p.fullName,
    age: p.age >= 18 ? String(p.age) : "",
    bio: p.bio,
    experience: p.yearsOfExperience > 0 ? String(p.yearsOfExperience) : "",
  };
}
export function addressFromForm(
  form: FormData,
  village: { villageId: string; villageLabel: string },
): AddressDraft {
  return {
    addressDetail: str(form.get("addressDetail")),
    postalCode: str(form.get("postalCode")),
    villageId: village.villageId,
    villageLabel: village.villageLabel,
  };
}
export function pristineAddress(data: ProviderAdmin): AddressDraft {
  return {
    addressDetail: data.addressDetail,
    postalCode: data.postalCode,
    villageId: data.villageId ?? "",
    villageLabel: data.provider.location ?? "",
  };
}
export function documentsFromForm(form: FormData): DocumentsDraft {
  const raw = form.get("file");
  const file = raw instanceof File && raw.size > 0 ? raw : undefined;
  return {
    documentType: str(form.get("documentType")) || "KTP",
    fileName: file ? file.name : "",
    file,
  };
}
export function pristineDocuments(): DocumentsDraft {
  return { documentType: "KTP", fileName: "" };
}
export const REQUIRED_DOCUMENT_TYPES = ["KTP", "KK"] as const;
export function missingDocumentType(
  documents: { documentType: string }[],
): string {
  return (
    REQUIRED_DOCUMENT_TYPES.find(
      (t) => !documents.some((d) => d.documentType === t),
    ) ?? "KTP"
  );
}
export function profileFromForm(form: FormData): ProfileDraft {
  return {
    categoryIds: form.getAll("categories").map(String).sort(),
    skills: normList(form.get("skills")),
    languages: normList(form.get("languages")),
    pricingType: str(form.get("pricingType")),
    price: normNum(form.get("price")),
    days: days.filter((d) => form.has(d)),
  };
}
export function pristineProfile(data: ProviderAdmin): ProfileDraft {
  const p = data.provider;
  return {
    categoryIds: p.categories.map((c) => c.id).sort(),
    skills: normList(p.skills.join(", ")),
    languages: normList(p.languages.join(", ")),
    pricingType: p.pricingType,
    price: normNum(p.price),
    days: days.filter((d) =>
      p.availability.some((a) => a.dayOfWeek === d && a.isAvailable),
    ),
  };
}
export function isSectionDirty(
  id: SectionId,
  draft: Drafts[SectionId] | undefined,
  data: ProviderAdmin,
): boolean {
  if (!draft) return false;
  switch (id) {
    case "personal":
      return !equal(draft as PersonalDraft, pristinePersonal(data));
    case "address": {
      const a = draft as AddressDraft;
      const p = pristineAddress(data);
      return !equal(
        {
          villageId: a.villageId,
          addressDetail: a.addressDetail,
          postalCode: a.postalCode,
        },
        {
          villageId: p.villageId,
          addressDetail: p.addressDetail,
          postalCode: p.postalCode,
        },
      );
    }
    case "documents": {
      const d = draft as DocumentsDraft;
      return d.fileName !== "";
    }
    case "profile":
      return !equal(draft as ProfileDraft, pristineProfile(data));
    default:
      return false;
  }
}

export function PersonalForm({
  data,
  save,
  busy,
  draft,
  onDraftChange,
}: FormProps) {
  const p = data.provider;
  const d = draft as PersonalDraft | undefined;
  const formRef = useRef<HTMLFormElement | null>(null);
  function capture() {
    if (!onDraftChange || !formRef.current) return;
    onDraftChange("personal", personalFromForm(new FormData(formRef.current)));
  }
  return (
    <form
      ref={formRef}
      onChange={capture}
      onSubmit={(e) => {
        e.preventDefault();
        const fd = new FormData(e.currentTarget);
        void save("personal-info", {
          fullName: fd.get("fullName"),
          age: Number(fd.get("age")),
          bio: fd.get("bio"),
          yearsOfExperience: Number(fd.get("experience")),
        });
      }}
    >
      <fieldset disabled={busy}>
        <Input
          label="Nama lengkap"
          name="fullName"
          defaultValue={d?.fullName ?? p.fullName}
          required
          minLength={2}
          maxLength={120}
        />
        <Input
          label="Usia"
          name="age"
          type="number"
          inputMode="numeric"
          min={18}
          max={80}
          placeholder="Contoh: 30"
          defaultValue={d?.age ?? (p.age >= 18 ? String(p.age) : "")}
          required
        />
        <Textarea
          label="Tentang penyedia"
          name="bio"
          defaultValue={d?.bio ?? p.bio}
          required
          minLength={10}
          maxLength={2000}
        />
        <Input
          label="Pengalaman (tahun)"
          name="experience"
          type="number"
          inputMode="numeric"
          min={0}
          max={62}
          placeholder="Contoh: 5"
          defaultValue={
            d?.experience ??
            (p.yearsOfExperience > 0 ? String(p.yearsOfExperience) : "")
          }
        />
        <Button type="submit" className="wide">
          Simpan informasi personal
        </Button>
      </fieldset>
    </form>
  );
}
export function ProviderAddressForm({
  data,
  save,
  busy,
  draft,
  onDraftChange,
}: FormProps) {
  const draftAddress = draft as AddressDraft | undefined;
  const initialId = draftAddress?.villageId ?? data.villageId ?? "";
  const initialLabel =
    draftAddress?.villageLabel ?? data.provider.location ?? "";
  const formRef = useRef<HTMLFormElement | null>(null);
  const villageRef = useRef({
    villageId: initialId,
    villageLabel: initialLabel,
  });
  const [villageId, setVillageId] = useState(initialId);
  const [label, setLabel] = useState(initialLabel);
  const [error, setError] = useState("");
  function capture(extra = villageRef.current) {
    if (!onDraftChange || !formRef.current) return;
    onDraftChange(
      "address",
      addressFromForm(new FormData(formRef.current), extra),
    );
  }
  return (
    <form
      ref={formRef}
      onChange={() => capture()}
      onSubmit={(e) => {
        e.preventDefault();
        if (!villageId) {
          setError("Pilih wilayah dari hasil pencarian.");
          return;
        }
        const fd = new FormData(e.currentTarget);
        void save("address", {
          villageId,
          addressDetail: fd.get("addressDetail"),
          postalCode: fd.get("postalCode"),
        });
      }}
    >
      <fieldset disabled={busy}>
        {data.provider.location && (
          <p>Wilayah tersimpan: {data.provider.location}</p>
        )}
        <AddressCombobox
          required={!villageId}
          initialLabel={label}
          onChange={(v) => {
            setError("");
            setVillageId(v?.villageId ?? "");
            setLabel(v?.displayLabel ?? "");
            const next = {
              villageId: v?.villageId ?? "",
              villageLabel: v?.displayLabel ?? "",
            };
            villageRef.current = next;
            capture(next);
          }}
        />
        <Textarea
          label="Detail alamat domisili"
          name="addressDetail"
          required
          minLength={10}
          maxLength={500}
          defaultValue={draftAddress?.addressDetail ?? data.addressDetail}
        />
        <Input
          label="Kode pos"
          name="postalCode"
          required
          pattern="[0-9]{5}"
          inputMode="numeric"
          maxLength={5}
          defaultValue={draftAddress?.postalCode ?? data.postalCode}
        />
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        <Button type="submit" className="wide">
          Simpan alamat
        </Button>
      </fieldset>
    </form>
  );
}
export function DocumentsForm({
  data,
  save,
  busy,
  downloadDocument,
  draft,
  onDraftChange,
}: FormProps) {
  const draftDoc = draft as DocumentsDraft | undefined;
  const formRef = useRef<HTMLFormElement | null>(null);
  const [error, setError] = useState("");
  const hasFile = Boolean(draftDoc?.file && draftDoc.file.size > 0);
  function capture() {
    if (!onDraftChange || !formRef.current) return;
    onDraftChange(
      "documents",
      documentsFromForm(new FormData(formRef.current)),
    );
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const body = new FormData(e.currentTarget);
    const picked = body.get("file");
    const pickedFile =
      picked instanceof File && picked.size > 0 ? picked : null;
    const file =
      pickedFile ??
      (draftDoc?.file && draftDoc.file.size > 0 ? draftDoc.file : null);
    if (
      !file ||
      file.size > 5 * 1024 * 1024 ||
      !["image/jpeg", "image/png"].includes(file.type)
    ) {
      setError("Gunakan JPG/PNG maksimal 5 MB.");
      return;
    }
    body.set("file", file);
    setError("");
    await save("documents", body);
  }
  async function download(id: string) {
    try {
      const blob = await (downloadDocument
        ? downloadDocument(id)
        : authApi.download(
            `/api/admin/providers/${data.provider.id}/documents/${id}`,
          ));
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "dokumen.jpg";
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Dokumen gagal dibuka.");
    }
  }
  return (
    <>
      <p>
        Unggah KTP dan KK. Foto minimal 100 × 100 piksel, JPG/PNG maksimal 5 MB.
        Dokumen hanya dapat dibuka admin yang berwenang.
      </p>
      {REQUIRED_DOCUMENT_TYPES.map((t) => {
        const doc = data.documents.find((d) => d.documentType === t);
        return (
          <p key={t}>
            {t} {doc ? "tersimpan" : "belum diunggah"}{" "}
            {doc && (
              <Button variant="ghost" onClick={() => download(doc.id)}>
                Unduh {t}
              </Button>
            )}
          </p>
        );
      })}
      <form
        key={
          data.documents
            .map((d) => `${d.documentType}:${d.id}:${d.uploadedAt}`)
            .sort()
            .join(",") || "none"
        }
        ref={formRef}
        onChange={capture}
        onSubmit={submit}
      >
        <fieldset disabled={busy}>
          <Select
            label="Jenis dokumen"
            name="documentType"
            defaultValue={
              draftDoc?.documentType ?? missingDocumentType(data.documents)
            }
          >
            <option>KTP</option>
            <option>KK</option>
          </Select>
          <Input
            label="Foto dokumen"
            name="file"
            type="file"
            accept="image/jpeg,image/png"
            required={!hasFile}
          />
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <Button type="submit" className="wide">
            Unggah dokumen
          </Button>
        </fieldset>
      </form>
    </>
  );
}
export function ProfileForm({
  data,
  save,
  busy,
  draft,
  onDraftChange,
}: FormProps) {
  const p = data.provider;
  const draftProfile = draft as ProfileDraft | undefined;
  const categories = useCategories();
  const formRef = useRef<HTMLFormElement | null>(null);
  const [error, setError] = useState("");
  const checkedDays = draftProfile?.days;
  function capture() {
    if (!onDraftChange || !formRef.current) return;
    onDraftChange("profile", profileFromForm(new FormData(formRef.current)));
  }
  return (
    <form
      ref={formRef}
      onChange={capture}
      onSubmit={(e) => {
        e.preventDefault();
        const fd = new FormData(e.currentTarget);
        const categoryIds = fd.getAll("categories").map(String);
        const availability = days.map((day) => ({
          dayOfWeek: day,
          isAvailable: fd.has(day),
        }));
        if (categoryIds.length === 0) {
          setError("Pilih minimal satu kategori layanan yang aktif.");
          return;
        }
        if (!availability.some((a) => a.isAvailable)) {
          setError("Pilih minimal satu hari aktif ketersediaan.");
          return;
        }
        setError("");
        void save("profile", {
          categoryIds,
          skills: String(fd.get("skills"))
            .split(",")
            .map((x) => x.trim())
            .filter(Boolean),
          languages: String(fd.get("languages"))
            .split(",")
            .map((x) => x.trim())
            .filter(Boolean),
          pricingType: fd.get("pricingType"),
          price: Number(fd.get("price")),
          availability,
        });
      }}
    >
      <fieldset disabled={busy}>
        <legend>Kategori layanan</legend>
        <ResourceState {...categories} reload={categories.retry} />
        {categories.categories.map((c) => (
          <label className="check-option" key={c.id}>
            <input
              type="checkbox"
              name="categories"
              value={c.id}
              defaultChecked={
                draftProfile
                  ? draftProfile.categoryIds.includes(c.id)
                  : p.categories.some((x) => x.id === c.id)
              }
            />
            {c.name}
          </label>
        ))}
        <Input
          label="Keahlian (pisahkan dengan koma)"
          name="skills"
          defaultValue={draftProfile?.skills ?? p.skills.join(", ")}
          maxLength={1600}
          required
        />
        <Input
          label="Bahasa (pisahkan dengan koma)"
          name="languages"
          defaultValue={draftProfile?.languages ?? p.languages.join(", ")}
          maxLength={800}
          required
        />
        <Select
          label="Model tarif"
          name="pricingType"
          defaultValue={draftProfile?.pricingType ?? p.pricingType}
        >
          <option value="PerVisit">Per kunjungan</option>
          <option value="PerMonth">Per bulan</option>
        </Select>
        <Input
          label="Tarif (Rp)"
          name="price"
          type="number"
          min={1}
          max={999999999}
          step="0.01"
          defaultValue={draftProfile?.price ?? (p.price || "")}
          required
        />
        <h3>Ketersediaan mingguan</h3>
        {days.map((day) => (
          <label key={day} className="check-option">
            <input
              type="checkbox"
              name={day}
              defaultChecked={
                checkedDays
                  ? checkedDays.includes(day)
                  : p.availability.some(
                      (a) => a.dayOfWeek === day && a.isAvailable,
                    )
              }
            />
            {dayNames[day]}
          </label>
        ))}
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        <Button type="submit" className="wide">
          Simpan profil layanan
        </Button>
      </fieldset>
    </form>
  );
}
export function VerificationForm({ data, save, busy }: FormProps) {
  const p = data.provider;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const fd = new FormData(e.currentTarget);
        void save("verify", {
          identityVerified: fd.has("identity"),
          backgroundCheckPassed: fd.has("background"),
          contractSigned: fd.has("contract"),
          status: fd.get("status"),
          note: fd.get("note"),
        });
      }}
    >
      <fieldset disabled={busy}>
        <p>
          Centang hanya pemeriksaan yang sudah dilakukan. Perubahan data
          penyedia akan mengembalikan status ke Pending dan mengharuskan
          pemeriksaan ulang.
        </p>
        {[
          {
            name: "identity",
            label: "Identitas telah diperiksa",
            value: p.identityVerified,
          },
          {
            name: "background",
            label: "Pemeriksaan latar belakang lulus",
            value: p.backgroundCheckPassed,
          },
          {
            name: "contract",
            label: "Kontrak telah ditandatangani",
            value: p.contractSigned,
          },
        ].map((c) => (
          <label className="check-option" key={c.name}>
            <input type="checkbox" name={c.name} defaultChecked={c.value} />
            {c.label}
          </label>
        ))}
        <Select
          label="Status verifikasi"
          name="status"
          defaultValue={p.verificationStatus}
        >
          <option value="Pending">Pending</option>
          <option value="Verified">Verified</option>
          <option value="Rejected">Rejected</option>
        </Select>
        <Textarea
          label="Catatan pemeriksaan / override"
          name="note"
          maxLength={1000}
        />
        <Button type="submit">Simpan hasil pemeriksaan</Button>
      </fieldset>
    </form>
  );
}
