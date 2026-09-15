import {
  ArrowRight,
  ArrowUpRight,
  Check,
  Search,
  Sparkles,
} from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { useAuth } from "../context/AuthContext";
import { useCategories } from "../hooks/useCategories";
import { AppShell } from "../components/AppShell";
import { Badge, Button, Card, Input } from "../components/ui";
import { CategoryIcon } from "../components/CategoryIcon";
export function DashboardPage() {
  const { session } = useAuth();
  const { categories, loading, error, retry } = useCategories();
  const [search, setSearch] = useState("");
  const matches = categories.filter((c) =>
    `${c.name} ${c.description}`
      .toLocaleLowerCase("id")
      .includes(search.toLocaleLowerCase("id")),
  );
  return (
    <AppShell>
      <div className="greeting">
        <span>
          Halo, {session!.user.fullName.split(" ")[0]}{" "}
          <span aria-hidden="true">/</span> Selamat datang!
        </span>
        <Badge>
          <Check aria-hidden="true" /> Profil lengkap
        </Badge>
      </div>
      <section className="hero lifted">
        <div>
          <Badge tone="accent" sticker>
            BANTU-BANTU, BANYAK BISA.
          </Badge>
          <h1>
            Butuh bantuan?
            <br />
            Ada jalan lebih ringan.
          </h1>
          <p>
            Dari urusan rumah sampai perjalanan. Temukan jenis jasa yang pas
            untuk keseharianmu.
          </p>
          <Link className="btn btn-secondary" to="/categories">
            Jelajahi layanan <ArrowUpRight aria-hidden="true" />
          </Link>
        </div>
        <div className="hero-art" aria-hidden="true">
          <div>
            <Sparkles />
          </div>
          <div>
            <ArrowUpRight />
          </div>
          <span>
            Less repot.
            <br />
            More hidup.
          </span>
        </div>
      </section>
      <section aria-labelledby="category-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">MULAI DARI SINI</p>
            <h2 id="category-title">Lagi butuh apa?</h2>
          </div>
          <Link className="text-link" to="/categories">
            Semua <ArrowRight aria-hidden="true" />
          </Link>
        </div>
        <div className="search-field">
          <Search aria-hidden="true" />
          <Input
            label="Cari layanan"
            placeholder="Cari bantuan untuk harimu…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        {loading ? (
          <p role="status" className="empty">
            Memuat layanan…
          </p>
        ) : error ? (
          <Card>
            <p role="alert">{error}</p>
            <Button onClick={retry}>Coba lagi</Button>
          </Card>
        ) : matches.length === 0 ? (
          <Card>
            <h3>Belum ada layanan yang cocok</h3>
            <p>Coba kata lain atau lihat lagi nanti.</p>
          </Card>
        ) : (
          <div className="category-grid">
            {matches.map((category) => (
              <Link
                key={category.id}
                className="category-link"
                to={`/categories?category=${encodeURIComponent(category.slug)}`}
              >
                <Card lifted>
                  <CategoryIcon name={category.iconKey} />
                  <h3>{category.name}</h3>
                  <ArrowUpRight className="corner-icon" aria-hidden="true" />
                </Card>
              </Link>
            ))}
          </div>
        )}
      </section>
      {!search && categories.some((c) => c.isFeatured) && (
        <section aria-labelledby="featured-title">
          <div className="section-heading">
            <div>
              <p className="eyebrow">KENALI PILIHANNYA</p>
              <h2 id="featured-title">Layanan pilihan</h2>
            </div>
          </div>
          <div className="recommendation-list">
            {categories
              .filter((c) => c.isFeatured)
              .map((category) => (
                <Card key={category.id}>
                  <div className="recommendation-icon">
                    <CategoryIcon name={category.iconKey} />
                  </div>
                  <div>
                    <h3>{category.name}</h3>
                    <p>{category.description}</p>
                    <Link
                      className="text-link"
                      to={`/categories?category=${encodeURIComponent(category.slug)}`}
                    >
                      Kenali layanan <ArrowRight aria-hidden="true" />
                    </Link>
                  </div>
                </Card>
              ))}
          </div>
        </section>
      )}
      <aside className="note-card">
        <Sparkles aria-hidden="true" />
        <p>
          <strong>Satu tempat, banyak bantuan.</strong>
          <br />
          Pilihan jasa terus berkembang mengikuti kebutuhanmu.
        </p>
      </aside>
    </AppShell>
  );
}
