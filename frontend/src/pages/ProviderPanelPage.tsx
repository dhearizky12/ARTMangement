import { useState, type FormEvent } from "react";
import { Badge, Button, Card, Input } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { ProviderLayout } from "../components/provider/ProviderLayout";
import { useResource } from "../hooks/useResource";
import { authApi } from "../api/authApi";
import {
  dayNames,
  days,
  money,
  priceUnit,
  providerApi,
} from "../api/marketplaceApi";

export function ProviderPanelPage() {
  const profile = useResource(providerApi.profile, "provider-profile");
  const orders = useResource(providerApi.orders, "provider-orders");
  const [availabilityBusy, setAvailabilityBusy] = useState(false);
  const [availabilityMessage, setAvailabilityMessage] = useState("");
  const [availabilityError, setAvailabilityError] = useState("");
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [passwordMessage, setPasswordMessage] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const p = profile.data;

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
      setPasswordError(e instanceof Error ? e.message : "Kata sandi gagal diubah.");
    } finally {
      setPasswordBusy(false);
    }
  }

  return (
    <ProviderLayout>
      <p className="eyebrow">RUANG KERJA PROVIDER</p>
      <h1>{p?.fullName || "Panel Provider"}</h1>
      <ResourceState {...profile} />
      {p && (
        <>
          <Card lifted>
            <div className="section-title">
              <h2>Profil Anda</h2>
              <Badge tone={p.verificationStatus === "Verified" ? "success" : "accent"}>
                {p.verificationStatus}
              </Badge>
            </div>
            <p>{p.bio || "Bio belum diisi oleh admin."}</p>
            <div className="tags">
              {p.categories.map((category) => <Badge key={category.id}>{category.name}</Badge>)}
              {p.skills.map((skill) => <Badge key={`skill-${skill}`}>{skill}</Badge>)}
              {p.languages.map((language) => <Badge key={`language-${language}`}>{language}</Badge>)}
            </div>
            <dl>
              <dt>Pengalaman</dt>
              <dd>{p.yearsOfExperience} tahun · {p.jobsCompletedCount} pekerjaan selesai</dd>
              <dt>Tarif</dt>
              <dd>{money(p.price)} / {priceUnit(p.pricingType)}</dd>
              <dt>Domisili</dt>
              <dd>{p.location || "Belum diisi oleh admin."}</dd>
            </dl>
          </Card>

          <Card>
            <h2>Ketersediaan mingguan</h2>
            <form onSubmit={saveAvailability}>
              <fieldset disabled={availabilityBusy}>
                <div className="availability">
                  {days.map((day) => (
                    <label className="check-option" key={day}>
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
                {availabilityError && <p className="error" role="alert">{availabilityError}</p>}
                {availabilityMessage && <p role="status">{availabilityMessage}</p>}
                <Button type="submit" disabled={availabilityBusy}>
                  {availabilityBusy ? "Menyimpan…" : "Simpan ketersediaan"}
                </Button>
              </fieldset>
            </form>
          </Card>

          <Card>
            <h2>Ganti kata sandi</h2>
            <form onSubmit={changePassword}>
              <fieldset disabled={passwordBusy}>
                <Input label="Kata sandi saat ini" name="currentPassword" type="password" required maxLength={256} />
                <Input label="Kata sandi baru" name="newPassword" type="password" required minLength={14} maxLength={256} autoComplete="new-password" />
                {passwordError && <p className="error" role="alert">{passwordError}</p>}
                {passwordMessage && <p role="status">{passwordMessage}</p>}
                <Button type="submit" disabled={passwordBusy}>
                  {passwordBusy ? "Menyimpan…" : "Ubah kata sandi"}
                </Button>
              </fieldset>
            </form>
          </Card>
        </>
      )}

      <section className="market-section">
        <h2>Pesanan Anda</h2>
        <ResourceState {...orders} />
        {orders.data?.length ? (
          <div className="provider-grid">
            {orders.data.map((order) => (
              <Card key={order.id} className="order-card">
                <Badge>{order.status}</Badge>
                <h3>{order.providerName}</h3>
                <p>{order.scheduledDate} · {money(order.price)}</p>
                <p className="muted">{order.addressDetail}</p>
              </Card>
            ))}
          </div>
        ) : orders.data ? (
          <Card><p>Belum ada pesanan yang ditugaskan kepada Anda.</p></Card>
        ) : null}
      </section>
    </ProviderLayout>
  );
}
