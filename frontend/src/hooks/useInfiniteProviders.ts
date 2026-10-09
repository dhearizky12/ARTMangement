import { useCallback, useEffect, useRef, useState } from "react";
import { marketplaceApi, type Provider } from "../api/marketplaceApi";

export function useInfiniteProviders(params: URLSearchParams) {
  const key = params.toString();
  const [items, setItems] = useState<Provider[]>([]);
  const [total, setTotal] = useState(0);
  const [pageSize, setPageSize] = useState(0);
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState("");
  const [moreError, setMoreError] = useState("");
  const seq = useRef(0);
  const moreInFlight = useRef(false);

  const hasMore = pageSize > 0 && page * pageSize < total;

  const loadPage = useCallback(
    async (targetPage: number, replace: boolean) => {
      if (!replace) {
        if (moreInFlight.current) return;
        moreInFlight.current = true;
      }
      const request = ++seq.current;
      if (replace) {
        setLoading(true);
        setError("");
      } else {
        setLoadingMore(true);
        setMoreError("");
      }
      try {
        const query = new URLSearchParams(key);
        query.set("page", String(targetPage));
        const data = await marketplaceApi.providers(query);
        if (request === seq.current) {
          setItems((prev) => (replace ? data.items : [...prev, ...data.items]));
          setTotal(data.total);
          setPageSize(data.pageSize);
          setPage(data.page);
        }
      } catch (e) {
        if (request === seq.current) {
          const message = e instanceof Error ? e.message : "Gagal memuat data.";
          if (replace) setError(message);
          else setMoreError(message);
        }
      } finally {
        if (!replace) moreInFlight.current = false;
        if (request === seq.current) {
          setLoading(false);
          setLoadingMore(false);
        }
      }
    },
    [key],
  );

  useEffect(() => {
    setItems([]);
    setTotal(0);
    setPageSize(0);
    setPage(0);
    void loadPage(1, true);
    return () => {
      seq.current++;
    };
  }, [loadPage]);

  const loadMore = useCallback(() => {
    if (loading || loadingMore || !hasMore) return;
    void loadPage(page + 1, false);
  }, [page, hasMore, loading, loadingMore, loadPage]);

  const retry = useCallback(() => {
    void loadPage(1, true);
  }, [loadPage]);

  return {
    items,
    total,
    hasMore,
    loading,
    loadingMore,
    error,
    moreError,
    loadMore,
    retry,
  };
}
