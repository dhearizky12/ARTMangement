import { AddressCombobox } from "../../components/AddressCombobox";
import type { VillageResult } from "../../api/wilayahApi";
import { useState, type FormEvent } from "react";
import { ArrowRight } from "lucide-react";
import { profileApi } from "../../api/profileApi";
import { ApiError } from "../../api/authApi";
import { useProfile } from "../../routes/ProfileBoundary";
import { Button, Input, Textarea } from "../../components/ui";
import { WizardLayout } from "./WizardLayout";
export function AddressStep() {
  const { status, sync } = useProfile();
  const [village, setVillage] = useState<VillageResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!village) {
      setError("Pilih kelurahan/desa dari hasil pencarian.");
      return;
    }
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await profileApi.address({
        villageId: village.villageId,
        addressDetail: String(data.get("addressLine")),
        postalCode: String(data.get("postalCode")),
      });
      await sync();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Alamat belum tersimpan.");
      if (e instanceof ApiError && e.status === 409)
        await sync().catch(() => {});
    } finally {
      setBusy(false);
    }
  }
  return (
    <WizardLayout
      title="Di mana bantuan dibutuhkan?"
      description="Tuliskan alamat lengkap agar informasi profilmu jelas."
    >
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <AddressCombobox onChange={setVillage} />
          <Textarea
            label="Alamat lengkap"
            name="addressLine"
            autoComplete="street-address"
            placeholder="Nama jalan, nomor rumah, RT/RW, dan patokan"
            rows={3}
            required
            minLength={10}
            maxLength={500}
            defaultValue={status.address?.addressLine}
          />
          <Input
            label="Kode pos"
            name="postalCode"
            autoComplete="postal-code"
            inputMode="numeric"
            pattern="[0-9]{5}"
            maxLength={5}
            required
            defaultValue={status.address?.postalCode}
          />
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <Button type="submit" className="wide" disabled={busy}>
            {busy ? "Menyimpan…" : "Lanjut ke dokumen"}
            <ArrowRight aria-hidden="true" />
          </Button>
        </fieldset>
      </form>
    </WizardLayout>
  );
}
