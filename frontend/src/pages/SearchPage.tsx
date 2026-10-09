import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { Input, Button, Card } from "../components/ui";
import { AddressCombobox } from "../components/AddressCombobox";
import { ProviderCard } from "../components/ProviderCard";
import { ProviderGridSkeleton } from "../components/skeletons";
import { ResourceState } from "../components/ResourceState";
import { useCategories } from "../hooks/useCategories";
import { useInfiniteProviders } from "../hooks/useInfiniteProviders";
export function SearchPage() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get("q") || "");
  const categories = useCategories();
  const {
    items,
    total,
    hasMore,
    loading,
    loadingMore,
    error,
    moreError,
    loadMore,
    retry,
  } = useInfiniteProviders(params);
  const sentinel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = sentinel.current;
    if (!node || !hasMore || moreError) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) loadMore();
      },
      { rootMargin: "400px" },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [hasMore, moreError, loadMore]);
  function update(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
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
          placeholder="Mis. taman, driver, atau nama penyedia"
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
      <ResourceState
        loading={loading}
        error={error}
        reload={retry}
        skeleton={<ProviderGridSkeleton />}
      />
      {!loading && !error && (
        <>
          <p>{total} penyedia ditemukan</p>
          <div className="provider-grid">
            {items.map((p) => (
              <ProviderCard key={p.id} provider={p} />
            ))}
          </div>
          {!total && (
            <Card>
              <h2>Belum ada yang sesuai</h2>
              <p>Coba nama, keahlian, kategori, atau wilayah lain.</p>
            </Card>
          )}
          {loadingMore && (
            <p role="status" className="load-more-status">
              Memuat layanan lainnya…
            </p>
          )}
          {moreError && (
            <div role="alert" className="load-more-error">
              <p>{moreError}</p>
              <Button onClick={loadMore}>Coba lagi</Button>
            </div>
          )}
          {hasMore && !moreError && (
            <div
              ref={sentinel}
              className="load-more-sentinel"
              aria-hidden="true"
            />
          )}
        </>
      )}
    </AppShell>
  );
}
