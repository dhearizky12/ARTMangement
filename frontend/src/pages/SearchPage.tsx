import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { Input, Button, Card } from "../components/ui";
import { AddressCombobox } from "../components/AddressCombobox";
import { ProviderCard } from "../components/ProviderCard";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { useCategories } from "../hooks/useCategories";
import { marketplaceApi } from "../api/marketplaceApi";
export function SearchPage() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get("q") || "");
  const categories = useCategories();
  const result = useResource(
    () => marketplaceApi.providers(params),
    params.toString(),
  );
  function update(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    if (key !== "page") next.delete("page");
    setParams(next);
  }
  return (
    <AppShell>
      <h1>Temukan bantuan Anda.</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          update("q", text);
        }}
        className="search-form"
      >
        <Input
          label="Nama atau keahlian"
          value={text}
          maxLength={100}
          onChange={(e) => setText(e.target.value)}
        />
        <Button type="submit">Cari</Button>
      </form>
      <details className="location-filter">
        <summary>
          Filter lokasi {params.has("villageId") ? "· aktif" : ""}
        </summary>
        <AddressCombobox
          onChange={(v) => update("villageId", v?.villageId || "")}
        />
      </details>
      <div className="filter-chips" aria-label="Kategori layanan">
        <Button
          variant={!params.has("category") ? "secondary" : "ghost"}
          onClick={() => update("category", "")}
        >
          Semua
        </Button>
        {categories.categories.map((c) => (
          <Button
            variant={params.get("category") === c.id ? "secondary" : "ghost"}
            key={c.id}
            onClick={() => update("category", c.id)}
            aria-pressed={params.get("category") === c.id}
          >
            {c.name}
          </Button>
        ))}
      </div>
      <ResourceState {...result} />
      {result.data && !result.loading && (
        <>
          <p>{result.data.total} penyedia ditemukan</p>
          <div className="provider-grid">
            {result.data.items.map((p) => (
              <ProviderCard key={p.id} provider={p} />
            ))}
          </div>
          {!result.data.total && (
            <Card>
              <h2>Belum ada yang sesuai</h2>
              <p>Coba nama, keahlian, kategori, atau wilayah lain.</p>
            </Card>
          )}
          <div className="pagination">
            <Button
              variant="ghost"
              disabled={result.data.page <= 1}
              onClick={() => update("page", String(result.data!.page - 1))}
            >
              Sebelumnya
            </Button>
            <span>Halaman {result.data.page}</span>
            <Button
              variant="ghost"
              disabled={
                result.data.page * result.data.pageSize >= result.data.total
              }
              onClick={() => update("page", String(result.data!.page + 1))}
            >
              Berikutnya
            </Button>
          </div>
        </>
      )}
    </AppShell>
  );
}
