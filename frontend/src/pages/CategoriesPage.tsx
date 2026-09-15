import { useSearchParams } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { Badge, Button, Card } from "../components/ui";
import { CategoryIcon } from "../components/CategoryIcon";
import { useCategories } from "../hooks/useCategories";
export function CategoriesPage() {
  const { categories, loading, error, retry } = useCategories();
  const [params, setParams] = useSearchParams();
  const selected = categories.find((c) => c.slug === params.get("category"));
  return (
    <AppShell>
      <p className="eyebrow">BERAGAM JASA, SATU TEMPAT</p>
      <h1>Pilih bantuanmu.</h1>
      <p className="muted">
        Kenali layanan untuk rumah, perjalanan, dan kebutuhan lainnya.
      </p>
      {loading ? (
        <p role="status">Memuat layanan…</p>
      ) : error ? (
        <Card>
          <p role="alert">{error}</p>
          <Button onClick={retry}>Coba lagi</Button>
        </Card>
      ) : (
        <>
          <div className="category-grid">
            {categories.map((c) => (
              <button
                key={c.id}
                className={`category-link category-button ${selected?.id === c.id ? "selected" : ""}`}
                aria-pressed={selected?.id === c.id}
                onClick={() => setParams({ category: c.slug })}
              >
                <Card lifted>
                  <CategoryIcon name={c.iconKey} />
                  <h3>{c.name}</h3>
                </Card>
              </button>
            ))}
          </div>
          {categories.length === 0 && (
            <Card>
              <h2>Layanan sedang disiapkan</h2>
              <p>Kembali lagi nanti untuk melihat kategori yang tersedia.</p>
            </Card>
          )}
          {selected && (
            <Card className="category-detail">
              <Badge tone="success">Kategori pilihan</Badge>
              <h2>{selected.name}</h2>
              <p>{selected.description}</p>
              <div className="empty">
                <h3>Daftar penyedia sedang disiapkan</h3>
                <p>
                  Pencarian dan pemesanan penyedia untuk layanan ini belum
                  tersedia.
                </p>
              </div>
            </Card>
          )}
        </>
      )}
    </AppShell>
  );
}
