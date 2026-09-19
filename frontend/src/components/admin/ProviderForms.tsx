import { useState, type FormEvent } from "react";
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
export interface FormProps {
  data: ProviderAdmin;
  save: (step: string, body: unknown) => Promise<void>;
  busy: boolean;
}
export function PersonalForm({ data, save, busy }: FormProps) {
  const p = data.provider;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const d = new FormData(e.currentTarget);
        void save("personal-info", {
          fullName: d.get("fullName"),
          age: Number(d.get("age")),
          bio: d.get("bio"),
          yearsOfExperience: Number(d.get("experience")),
        });
      }}
    >
      <fieldset disabled={busy}>
        <Input
          label="Nama lengkap"
          name="fullName"
          defaultValue={p.fullName}
          required
          minLength={2}
          maxLength={120}
        />
        <Input
          label="Usia"
          name="age"
          type="number"
          min={18}
          max={80}
          defaultValue={p.age || 18}
          required
        />
        <Textarea
          label="Tentang penyedia"
          name="bio"
          defaultValue={p.bio}
          required
          minLength={10}
          maxLength={2000}
        />
        <Input
          label="Pengalaman (tahun)"
          name="experience"
          type="number"
          min={0}
          max={62}
          defaultValue={p.yearsOfExperience}
          required
        />
        <Button type="submit">Simpan informasi personal</Button>
      </fieldset>
    </form>
  );
}
export function ProviderAddressForm({ data, save, busy }: FormProps) {
  const [village, setVillage] = useState<VillageResult | null>(null);
  const [changed, setChanged] = useState(false);
  const [error, setError] = useState("");
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const id = village?.villageId || (!changed ? data.villageId : null);
        if (!id) {
          setError("Pilih wilayah dari hasil pencarian.");
          return;
        }
        const d = new FormData(e.currentTarget);
        void save("address", {
          villageId: id,
          addressDetail: d.get("addressDetail"),
          postalCode: d.get("postalCode"),
        });
      }}
    >
      <fieldset disabled={busy}>
        {data.provider.location && (
          <p>Wilayah tersimpan: {data.provider.location}</p>
        )}
        <AddressCombobox
          required={!data.villageId || changed}
          onChange={(v) => {
            setChanged(true);
            setVillage(v);
            setError("");
          }}
        />
        <Textarea
          label="Detail alamat domisili"
          name="addressDetail"
          required
          minLength={10}
          maxLength={500}
          defaultValue={data.addressDetail}
        />
        <Input
          label="Kode pos"
          name="postalCode"
          required
          pattern="[0-9]{5}"
          inputMode="numeric"
          maxLength={5}
          defaultValue={data.postalCode}
        />
        {error && <p role="alert">{error}</p>}
        <Button type="submit">Simpan alamat</Button>
      </fieldset>
    </form>
  );
}
export function DocumentsForm({ data, save, busy }: FormProps) {
  const [error, setError] = useState("");
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const body = new FormData(e.currentTarget);
    const file = body.get("file") as File;
    if (
      !file ||
      file.size > 5 * 1024 * 1024 ||
      !["image/jpeg", "image/png"].includes(file.type)
    ) {
      setError("Gunakan JPG/PNG maksimal 5 MB.");
      return;
    }
    setError("");
    await save("documents", body);
  }
  async function download(id: string) {
    try {
      const blob = await authApi.download(
        `/api/admin/providers/${data.provider.id}/documents/${id}`,
      );
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
      {data.documents.map((d) => (
        <p key={d.id}>
          {d.documentType} tersimpan{" "}
          <Button variant="ghost" onClick={() => download(d.id)}>
            Unduh {d.documentType}
          </Button>
        </p>
      ))}
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <Select label="Jenis dokumen" name="documentType">
            <option>KTP</option>
            <option>KK</option>
          </Select>
          <Input
            label="Foto dokumen"
            name="file"
            type="file"
            accept="image/jpeg,image/png"
            required
          />
          {error && <p role="alert">{error}</p>}
          <Button type="submit">Unggah dokumen</Button>
        </fieldset>
      </form>
    </>
  );
}
export function ProfileForm({ data, save, busy }: FormProps) {
  const p = data.provider;
  const categories = useCategories();
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const d = new FormData(e.currentTarget);
        void save("profile", {
          categoryIds: d.getAll("categories"),
          skills: String(d.get("skills"))
            .split(",")
            .map((x) => x.trim())
            .filter(Boolean),
          languages: String(d.get("languages"))
            .split(",")
            .map((x) => x.trim())
            .filter(Boolean),
          pricingType: d.get("pricingType"),
          price: Number(d.get("price")),
          availability: days.map((day) => ({
            dayOfWeek: day,
            isAvailable: d.has(day),
          })),
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
              defaultChecked={p.categories.some((x) => x.id === c.id)}
            />
            {c.name}
          </label>
        ))}
        <Input
          label="Keahlian (pisahkan dengan koma)"
          name="skills"
          defaultValue={p.skills.join(", ")}
          maxLength={1600}
          required
        />
        <Input
          label="Bahasa (pisahkan dengan koma)"
          name="languages"
          defaultValue={p.languages.join(", ")}
          maxLength={800}
          required
        />
        <Select
          label="Model tarif"
          name="pricingType"
          defaultValue={p.pricingType}
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
          defaultValue={p.price || ""}
          required
        />
        <h3>Ketersediaan mingguan</h3>
        {days.map((day) => (
          <label key={day} className="check-option">
            <input
              type="checkbox"
              name={day}
              defaultChecked={p.availability.some(
                (a) => a.dayOfWeek === day && a.isAvailable,
              )}
            />
            {dayNames[day]}
          </label>
        ))}
        <Button type="submit">Simpan profil layanan</Button>
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
        const d = new FormData(e.currentTarget);
        void save("verify", {
          identityVerified: d.has("identity"),
          backgroundCheckPassed: d.has("background"),
          contractSigned: d.has("contract"),
          status: d.get("status"),
          note: d.get("note"),
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
