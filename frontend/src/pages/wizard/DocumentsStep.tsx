import { useEffect, useState, type FormEvent } from "react";
import { Check, FileImage, Upload } from "lucide-react";
import { profileApi } from "../../api/profileApi";
import { ApiError } from "../../api/authApi";
import { useProfile } from "../../routes/ProfileBoundary";
import { Button, Select } from "../../components/ui";
import { WizardLayout } from "./WizardLayout";
export function DocumentsStep() {
  const { status, sync } = useProfile();
  const rules = status.documentRules;
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!file) {
      setPreview("");
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file) {
      setError("Pilih foto dokumen terlebih dahulu.");
      return;
    }
    const data = new FormData(event.currentTarget);
    data.set("file", file);
    setBusy(true);
    setError("");
    try {
      await profileApi.documents(data);
      await sync();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Dokumen belum tersimpan.");
      if (e instanceof ApiError && e.status === 409)
        await sync().catch(() => {});
    } finally {
      setBusy(false);
    }
  }
  return (
    <WizardLayout
      title="Satu langkah lagi!"
      description="Unggah foto identitas yang jelas dan dapat dibaca."
    >
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <Select
            label="Jenis dokumen"
            name="documentType"
            required
            defaultValue={rules.documentTypes[0]}
          >
            {rules.documentTypes.map((type) => (
              <option key={type} value={type}>
                {type === "Passport" ? "Paspor" : type}
              </option>
            ))}
          </Select>
          <div className="upload-zone">
            <Upload aria-hidden="true" />
            <label htmlFor="document-file">Pilih foto dokumen</label>
            <p className="muted">
              JPG atau PNG, maksimal {rules.maxBytes / 1024 / 1024} MB.
              <br />
              Minimal 100 × 100, maksimal 20 megapiksel.
            </p>
            <input
              id="document-file"
              name="file"
              type="file"
              accept={rules.contentTypes.join(",")}
              required
              onChange={(event) => {
                const chosen = event.target.files?.[0];
                setError("");
                setFile(null);
                if (!chosen) return;
                if (
                  !rules.contentTypes.includes(chosen.type) ||
                  chosen.size <= 0 ||
                  chosen.size > rules.maxBytes
                ) {
                  setError("Pilih foto JPG/PNG dengan ukuran maksimal 5 MB.");
                  event.target.value = "";
                  return;
                }
                setFile(chosen);
              }}
            />
          </div>
          {file && (
            <div className="file-preview">
              {preview && (
                <img src={preview} alt="Pratinjau dokumen yang akan diunggah" />
              )}
              <p>
                <FileImage aria-hidden="true" />
                {file.name}
              </p>
            </div>
          )}
          <p className="caption muted">
            Kelengkapan profil akan aktif setelah file diterima. Pemeriksaan
            identitas dilakukan terpisah.
          </p>
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          <Button type="submit" className="wide" disabled={busy || !file}>
            {busy ? "Mengunggah…" : "Selesaikan profil"}
            <Check aria-hidden="true" />
          </Button>
        </fieldset>
      </form>
    </WizardLayout>
  );
}
