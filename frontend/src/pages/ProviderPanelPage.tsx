import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  Input,
  PasswordInput,
  Spinner,
} from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { ProviderLayout } from "../components/provider/ProviderLayout";
import { useResource } from "../hooks/useResource";
import { authApi } from "../api/authApi";
import { Check } from "lucide-react";
import {
  days,
  dayNames,
  money,
  priceUnit,
  providerApi,
} from "../api/marketplaceApi";

const tabs = [
  { id: "ringkasan", label: "Ringkasan" },
  { id: "ketersediaan", label: "Ketersediaan" },
  { id: "pesanan", label: "Pesanan" },
  { id: "pengaturan", label: "Pengaturan" },
] as const;

const sectionLabels: Record<string, string> = {
  personal: "Personal",
  address: "Alamat",
  documents: "Dokumen",
  profile: "Layanan",
};

const sectionStatusLabels: Record<string, string> = {
  belum: "Belum diisi",
  sebagian: "Sebagian",
  lengkapi: "Lengkap",
};

export function ProviderPanelPage() {
  const profile = useResource(providerApi.profile, "provider-profile");
  const application = useResource(
    providerApi.application,
    "provider-application",
  );
  const orders = useResource(providerApi.orders, "provider-orders");
  const [params] = useSearchParams();
  const tab = params.get("tab") || "ringkasan";
  const [availabilityBusy, setAvailabilityBusy] = useState(false);
  const [availabilityMessage, setAvailabilityMessage] = useState("");
  const [availabilityError, setAvailabilityError] = useState("");
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [passwordMessage, setPasswordMessage] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const p = profile.data;
  const sections = application.data?.sections ?? [];
  const doneCount = sections.filter((s) => s.status === "lengkapi").length;
  const firstIncomplete = sections.find((s) => s.status !== "lengkapi");
  const canEdit = application.data?.canEdit ?? false;

  async function saveAvailability(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setAvailabilityBusy(true);
    setAvailabilityError("");
    setAvailabilityMessage("");
    try {
      await providerApi.availability(
        days.map((day) => ({ dayOfWeek: day, isAvailable: form.has(day) })),
      );
      await profile.reload();
      setAvailabilityMessage("Ketersediaan tersimpan.");
    } catch (e) {
      setAvailabilityError(
        e instanceof Error ? e.message : "Ketersediaan gagal disimpan.",
      );
    } finally {
      setAvailabilityBusy(false);
    }
  }

  async function changePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setPasswordBusy(true);
    setPasswordError("");
    setPasswordMessage("");
    try {
      await authApi.changeProviderPassword(
        String(form.get("currentPassword")),
        String(form.get("newPassword")),
      );
      event.currentTarget.reset();
      setPasswordMessage("Kata sandi berhasil diubah.");
    } catch (e) {
      setPasswordError(
        e instanceof Error ? e.message : "Kata sandi gagal diubah.",
      );
    } finally {
      setPasswordBusy(false);
    }
  }

  return (
    <ProviderLayout>
      <p className="eyebrow">RUANG KERJA PROVIDER</p>
      <h1>{p?.fullName || "Panel Provider"}</h1>
      <ResourceState {...profile} />
      <nav className="panel-tabs" aria-label="Tab panel Provider">
        {tabs.map((t) => (
          <Link
            key={t.id}
            to={`/provider/dashboard?tab=${t.id}`}
            className={t.id === tab ? "active" : ""}
            aria-current={t.id === tab ? "page" : undefined}
          >
            {t.label}
          </Link>
        ))}
      </nav>
      {p && tab === "ringkasan" && (
        <Card lifted>
          <div className="section-title">
            <h2>Profil Anda</h2>
            <div className="tags">
              <Badge
                tone={p.applicationStatus === "Approved" ? "success" : "accent"}
              >
                {p.applicationStatus}
              </Badge>
              <Badge
                tone={
                  p.verificationStatus === "Verified" ? "success" : "accent"
                }
              >
                {p.verificationStatus}
              </Badge>
            </div>
          </div>
          {canEdit && sections.length > 0 ? (
            <div className="profile-checklist">
              <div className="checklist-head">
                <span>Progres aplikasi</span>
                <strong>
                  {doneCount}/{sections.length}
                </strong>
              </div>
              {sections.map((s) => (
                <Link
                  key={s.id}
                  className={`checklist-item ${s.status === "lengkapi" ? "complete" : ""}`}
                  to={`/provider/onboarding?step=${s.id}`}
                >
                  <span className="checklist-icon">
                    {s.status === "lengkapi" && <Check aria-hidden="true" />}
                  </span>
                  {sectionLabels[s.id]}
                  <span className="checklist-status">
                    {sectionStatusLabels[s.status]}
                  </span>
                </Link>
              ))}
              {firstIncomplete && (
                <Link
                  className="btn btn-secondary wide"
                  to={`/provider/onboarding?step=${firstIncomplete.id}`}
                >
                  Lanjutkan: {sectionLabels[firstIncomplete.id]}
                </Link>
              )}
            </div>
          ) : p.applicationStatus === "Submitted" ? (
            <p className="muted">Aplikasi Anda sedang ditinjau oleh admin.</p>
          ) : p.applicationStatus === "Rejected" ? (
            <p className="error">
              Aplikasi ditolak. Catatan admin:{" "}
              {p.moderationNote || "Silakan hubungi admin."}
            </p>
          ) : p.applicationStatus === "Suspended" ? (
            <p className="error">Akun Provider sedang ditangguhkan.</p>
          ) : null}
          {p.bio ? (
            <p>{p.bio}</p>
          ) : (
            <p>
              <Link
                className="text-link"
                to="/provider/onboarding?step=personal"
              >
                Bio belum diisi.
              </Link>
            </p>
          )}
          <div className="tags">
            {p.categories.map((category) => (
              <Badge key={category.id}>{category.name}</Badge>
            ))}
            {p.skills.map((skill) => (
              <Badge key={`skill-${skill}`}>{skill}</Badge>
            ))}
            {p.languages.map((language) => (
              <Badge key={`language-${language}`}>{language}</Badge>
            ))}
          </div>
          <dl>
            <dt>Pengalaman</dt>
            <dd>
              {p.yearsOfExperience > 0 || p.jobsCompletedCount > 0 ? (
                <>
                  {p.yearsOfExperience} tahun · {p.jobsCompletedCount} pekerjaan
                  selesai
                </>
              ) : (
                <Link
                  className="text-link"
                  to="/provider/onboarding?step=personal"
                >
                  Belum diisi
                </Link>
              )}
            </dd>
            <dt>Tarif</dt>
            <dd>
              {p.price > 0 ? (
                <>
                  {money(p.price)} / {priceUnit(p.pricingType)}
                </>
              ) : (
                <Link
                  className="text-link"
                  to="/provider/onboarding?step=profile"
                >
                  Belum diisi
                </Link>
              )}
            </dd>
            <dt>Domisili</dt>
            <dd>
              {p.location || (
                <Link
                  className="text-link"
                  to="/provider/onboarding?step=address"
                >
                  Belum diisi
                </Link>
              )}
            </dd>
          </dl>
        </Card>
      )}

      {p && tab === "ketersediaan" && (
        <Card>
          <h2>Ketersediaan mingguan</h2>
          <form onSubmit={saveAvailability}>
            <fieldset disabled={availabilityBusy}>
              <div className="availability-chips">
                {days.map((day) => (
                  <label
                    className={`chip ${p.availability.some((item) => item.dayOfWeek === day && item.isAvailable) ? "active" : ""}`}
                    key={day}
                  >
                    <input
                      type="checkbox"
                      name={day}
                      defaultChecked={p.availability.some(
                        (item) => item.dayOfWeek === day && item.isAvailable,
                      )}
                    />
                    {dayNames[day]}
                  </label>
                ))}
              </div>
              {availabilityError && (
                <p className="error" role="alert">
                  {availabilityError}
                </p>
              )}
              {availabilityMessage && (
                <p role="status">{availabilityMessage}</p>
              )}
              <Button type="submit" disabled={availabilityBusy}>
                {availabilityBusy && <Spinner />}
                {availabilityBusy ? "Menyimpan…" : "Simpan ketersediaan"}
              </Button>
            </fieldset>
          </form>
        </Card>
      )}

      {tab === "pesanan" && (
        <section className="market-section">
          <h2>Pesanan Anda</h2>
          <ResourceState {...orders} />
          {orders.data?.length ? (
            <div className="provider-grid">
              {orders.data.map((order) => (
                <Card key={order.id} className="order-card">
                  <Badge>{order.status}</Badge>
                  <h3>{order.providerName}</h3>
                  <p>
                    {order.scheduledDate} · {money(order.price)}
                  </p>
                  <p className="muted">{order.addressDetail}</p>
                </Card>
              ))}
            </div>
          ) : orders.data ? (
            <Card>
              <p>Belum ada pesanan yang ditugaskan kepada Anda.</p>
            </Card>
          ) : null}
        </section>
      )}

      {tab === "pengaturan" && (
        <Card>
          <h2>Ganti kata sandi</h2>
          <form onSubmit={changePassword}>
            <fieldset disabled={passwordBusy}>
              <PasswordInput
                label="Kata sandi saat ini"
                name="currentPassword"
                autoComplete="current-password"
                required
                maxLength={256}
              />
              <PasswordInput
                label="Kata sandi baru"
                name="newPassword"
                autoComplete="new-password"
                required
                minLength={14}
                maxLength={256}
              />
              {passwordError && (
                <p className="error" role="alert">
                  {passwordError}
                </p>
              )}
              {passwordMessage && <p role="status">{passwordMessage}</p>}
              <Button type="submit" disabled={passwordBusy}>
                {passwordBusy && <Spinner />}
                {passwordBusy ? "Menyimpan…" : "Ubah kata sandi"}
              </Button>
            </fieldset>
          </form>
        </Card>
      )}
    </ProviderLayout>
  );
}
