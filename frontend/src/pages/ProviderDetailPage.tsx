import { useState, type FormEvent } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { Check, Minus } from "lucide-react";
import { AppShell } from "../components/AppShell";
import { Card, Badge, Button, Input, Textarea } from "../components/ui";
import { AddressCombobox } from "../components/AddressCombobox";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { useAuth } from "../context/AuthContext";
import {
  marketplaceApi,
  money,
  priceUnit,
  dayNames,
} from "../api/marketplaceApi";
import type { VillageResult } from "../api/wilayahApi";
export function ProviderDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { session } = useAuth();
  const result = useResource(() => marketplaceApi.provider(id!), id!);
  const [booking, setBooking] = useState(false);
  const [village, setVillage] = useState<VillageResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const p = result.data;
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!village) {
      setError("Pilih kelurahan/desa lokasi layanan.");
      return;
    }
    const data = new FormData(e.currentTarget);
    setBusy(true);
    setError("");
    try {
      await marketplaceApi.book({
        providerId: id,
        scheduledDate: data.get("scheduledDate"),
        villageId: village.villageId,
        addressDetail: data.get("addressDetail"),
      });
      navigate("/orders");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Pesanan gagal.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <AppShell>
      <Link className="text-link" to="/search">
        ← Kembali ke pencarian
      </Link>
      <ResourceState {...result} />
      {p && (
        <>
          <section className="provider-detail-hero">
            <span className="avatar large" aria-hidden="true">
              {p.fullName
                .split(" ")
                .slice(0, 2)
                .map((n) => n[0])
                .join("")}
            </span>
            <div>
              <Badge tone="success" sticker>
                Terverifikasi
              </Badge>
              <h1>{p.fullName}</h1>
              <p>{p.categories.map((c) => c.name).join(" · ")}</p>
              <p className="muted">{p.location}</p>
              <p>{p.agencyName || "Talenta langsung Bantu-Bantu"}</p>
            </div>
          </section>
          <div className="stats-grid">
            <Card>
              <strong>{p.yearsOfExperience} tahun</strong>
              <span>Pengalaman</span>
            </Card>
            <Card>
              <strong>{p.rating?.toFixed(1) || "—"}</strong>
              <span>{p.reviewCount} ulasan</span>
            </Card>
            <Card>
              <strong>{p.jobsCompletedCount}</strong>
              <span>Pesanan selesai</span>
            </Card>
          </div>
          <section className="market-section">
            <h2>Tentang {p.fullName}</h2>
            <p className="preserve-lines">{p.bio}</p>
            <p>{p.age} tahun</p>
          </section>
          <Card>
            <h2>Pemeriksaan penyedia</h2>
            {[
              ["Identitas diperiksa", p.identityVerified],
              ["Latar belakang diperiksa", p.backgroundCheckPassed],
              ["Kontrak ditandatangani", p.contractSigned],
            ].map(([label, yes]) => (
              <p className="check-row" key={String(label)}>
                {yes ? <Check /> : <Minus />}
                {label}
              </p>
            ))}
            <Link to="/trust">Tentang proses verifikasi</Link>
          </Card>
          <section className="market-section">
            <h2>Keahlian</h2>
            <div className="tags">
              {p.skills.map((s) => (
                <Badge key={s}>{s}</Badge>
              ))}
            </div>
            <h2>Bahasa</h2>
            <div className="tags">
              {p.languages.map((s) => (
                <Badge key={s}>{s}</Badge>
              ))}
            </div>
            <h2>Ketersediaan mingguan</h2>
            <div className="availability">
              {p.availability.map((d) => (
                <div
                  key={d.dayOfWeek}
                  className={d.isAvailable ? "available" : ""}
                >
                  <strong>{dayNames[d.dayOfWeek]}</strong>
                  <span>{d.isAvailable ? "Tersedia" : "Libur"}</span>
                </div>
              ))}
            </div>
          </section>
          <section className="market-section">
            <h2>Ulasan dari pesanan selesai</h2>
            {p.reviews.length ? (
              p.reviews.map((r, i) => (
                <Card key={i}>
                  <strong>{r.rating}/5</strong>
                  <p>{r.comment}</p>
                  <small>
                    {new Date(r.createdAt).toLocaleDateString("id-ID")}
                  </small>
                </Card>
              ))
            ) : (
              <p>Belum ada ulasan.</p>
            )}
          </section>
          {booking && session?.user.role === "Customer" && (
            <Card id="booking">
              <h2>Konfirmasi pesanan</h2>
              <p>
                {money(p.price)} / {priceUnit(p.pricingType)}. Pesanan akan
                menunggu konfirmasi admin.
              </p>
              <form onSubmit={submit}>
                <fieldset disabled={busy}>
                  <Input
                    name="scheduledDate"
                    label="Tanggal mulai layanan"
                    type="date"
                    min={new Date(Date.now() + 86400000)
                      .toISOString()
                      .slice(0, 10)}
                    required
                  />
                  <AddressCombobox onChange={setVillage} />
                  <Textarea
                    name="addressDetail"
                    label="Detail alamat layanan"
                    required
                    minLength={10}
                    maxLength={500}
                  />
                  {error && (
                    <p role="alert" className="error">
                      {error}
                    </p>
                  )}
                  <Button type="submit" disabled={busy}>
                    {busy ? "Menyimpan…" : "Konfirmasi pesanan"}
                  </Button>
                </fieldset>
              </form>
            </Card>
          )}
          <div className="booking-bar">
            <strong>
              {money(p.price)}
              <small> /{priceUnit(p.pricingType)}</small>
            </strong>
            {!session ? (
              <Link
                className="btn btn-primary"
                to={`/login?returnTo=${encodeURIComponent(`/providers/${id}`)}`}
              >
                Masuk & Pesan
              </Link>
            ) : session.user.role === "Customer" ? (
              <Button
                onClick={() => {
                  setBooking(true);
                  setTimeout(
                    () =>
                      document
                        .getElementById("booking")
                        ?.scrollIntoView({ behavior: "smooth" }),
                    0,
                  );
                }}
              >
                Pesan layanan
              </Button>
            ) : (
              <Link className="btn btn-secondary" to="/admin">
                Kelola di admin
              </Link>
            )}
          </div>
        </>
      )}
    </AppShell>
  );
}
