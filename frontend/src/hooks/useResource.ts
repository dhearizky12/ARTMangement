import { useCallback, useEffect, useRef, useState } from "react";
export function useResource<T>(fetcher: () => Promise<T>, key: string) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const fn = useRef(fetcher);
  fn.current = fetcher;
  const seq = useRef(0);
  const reload = useCallback(async (): Promise<T | undefined> => {
    const request = ++seq.current;
    setLoading(true);
    setError("");
    try {
      const value = await fn.current();
      if (request === seq.current) setData(value);
      return value;
    } catch (e) {
      if (request === seq.current)
        setError(e instanceof Error ? e.message : "Gagal memuat data.");
      return undefined;
    } finally {
      if (request === seq.current) setLoading(false);
    }
  }, [key]);
  useEffect(() => {
    setData(null);
    reload();
    return () => {
      seq.current++;
    };
  }, [reload]);
  return { data, error, loading, reload };
}
