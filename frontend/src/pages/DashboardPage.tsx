import { Link, useNavigate } from "react-router-dom";
import { Search, ShieldCheck, ArrowRight } from "lucide-react";
import { AppShell } from "../components/AppShell";
import { Card, Input, Button } from "../components/ui";
import { ProviderCard } from "../components/ProviderCard";
import { ResourceState } from "../components/ResourceState";
import { CategoryIcon } from "../components/CategoryIcon";
import { useCategories } from "../hooks/useCategories";
import { useResource } from "../hooks/useResource";
import { marketplaceApi } from "../api/marketplaceApi";
import { useAuth } from "../context/AuthContext";
export function DashboardPage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const categories = useCategories();
  const providers = useResource(
    () => marketplaceApi.providers(new URLSearchParams({ pageSize: "4" })),
    "home",
  );
  return (
    <AppShell>
      <section className="market-hero">
        <p className="eyebrow">
          {session
            ? `Halo, ${session.user.fullName}`
            : "Selamat datang di Bantu-Bantu"}
        </p>
        <h1>
          Bantuan untuk
          <br />
          hari-hari Anda.
        </h1>
        <p>Temukan penyedia jasa sesuai kebutuhan dan lokasi Anda.</p>
        <form
          className="search-form"
          onSubmit={(e) => {
            e.preventDefault();
            const q = new FormData(e.currentTarget).get("q");
            navigate(`/search?q=${encodeURIComponent(String(q))}`);
          }}
        >
          <Input
            label="Cari layanan atau penyedia"
            name="q"
            placeholder="Apa yang Anda butuhkan?"
            maxLength={100}
          />
          <Button type="submit" aria-label="Cari">
            <Search />
          </Button>
        </form>
        <Link className="text-link" to="/search">
          Pilih lokasi layanan <ArrowRight size={18} />
        </Link>
      </section>
      <Link to="/trust" className="trust-banner">
        <ShieldCheck />
        <span>Kenali pemeriksaan di balik lencana terverifikasi</span>
        <ArrowRight />
      </Link>
      <section className="market-section">
        <h2>Bantuan yang Anda cari</h2>
        <ResourceState {...categories} reload={categories.retry} />
        <div className="category-grid">
          {categories.categories.map((c) => (
            <Link
              className="card category-link"
              key={c.id}
              to={`/search?category=${c.id}`}
            >
              <CategoryIcon name={c.iconKey} />
              <strong>{c.name}</strong>
            </Link>
          ))}
        </div>
      </section>
      <section className="market-section">
        <div className="section-title">
          <h2>Pilihan penyedia</h2>
          <Link to="/search">Lihat semua</Link>
        </div>
        <p className="muted">
          Diurutkan berdasarkan ulasan dari pesanan yang telah selesai.
        </p>
        <ResourceState {...providers} />
        <div className="provider-grid">
          {providers.data?.items.map((p) => (
            <ProviderCard key={p.id} provider={p} />
          ))}
        </div>
        {providers.data?.total === 0 && (
          <Card>
            <h3>Penyedia sedang dipersiapkan</h3>
            <p>
              Profil akan tampil setelah proses verifikasi selesai. Anda tetap
              bisa menjelajahi kategori layanan.
            </p>
          </Card>
        )}
      </section>
    </AppShell>
  );
}
