import { useState, type FormEvent } from "react";
import { ArrowRight } from "lucide-react";
import { profileApi } from "../../api/profileApi";
import { ApiError } from "../../api/authApi";
import { useAuth } from "../../context/AuthContext";
import { useProfile } from "../../routes/ProfileBoundary";
import { Button, Input, Select } from "../../components/ui";
import { WizardLayout } from "./WizardLayout";
export function PersonalInfoStep() {
  const { status, sync } = useProfile();
  const { session } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await profileApi.personal({
        fullName: String(data.get("fullName")),
        birthDate: String(data.get("birthDate")),
        phoneNumber: String(data.get("phoneNumber")),
        gender: String(data.get("gender")),
      });
      await sync();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Data belum tersimpan.");
      if (e instanceof ApiError && e.status === 409)
        await sync().catch(() => {});
    } finally {
      setBusy(false);
    }
  }
  return (
    <WizardLayout
      title="Pertama, kenalan dulu."
      description="Isi data personal sesuai identitasmu."
    >
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <Input
            label="Nama lengkap"
            name="fullName"
            autoComplete="name"
            required
            minLength={2}
            maxLength={120}
            defaultValue={
              status.personalInfo?.fullName || session!.user.fullName
            }
          />
          <Input
            label="Tanggal lahir"
            name="birthDate"
            type="date"
            autoComplete="bday"
            required
            min="1900-01-01"
            max={new Date(Date.now() - 86400000).toISOString().slice(0, 10)}
            defaultValue={status.personalInfo?.birthDate}
          />
          <Input
            label="Nomor HP"
            name="phoneNumber"
            type="tel"
            autoComplete="tel"
            required
            pattern="(\+62|62|0)8[0-9]{8,12}"
            placeholder="081234567890"
            hint="Gunakan awalan 08 atau +628, tanpa spasi."
            defaultValue={status.personalInfo?.phoneNumber}
          />
          <Select
            label="Jenis kelamin"
            name="gender"
            defaultValue={status.personalInfo?.gender || ""}
            required
          >
            <option value="" disabled>
              Pilih jenis kelamin
            </option>
            <option value="female">Perempuan</option>
            <option value="male">Laki-laki</option>
            <option value="other">Lainnya</option>
            <option value="undisclosed">Tidak ingin menyebutkan</option>
          </Select>
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <Button type="submit" className="wide" disabled={busy}>
            {busy ? "Menyimpan…" : "Lanjut ke alamat"}
            <ArrowRight aria-hidden="true" />
          </Button>
        </fieldset>
      </form>
    </WizardLayout>
  );
}
