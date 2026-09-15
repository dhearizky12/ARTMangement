import { useCallback, useEffect, useState } from "react";
import { categoryApi, type ServiceCategory } from "../api/categoryApi";
export function useCategories() {
  const [categories, setCategories] = useState<ServiceCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setCategories(await categoryApi.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Layanan belum dapat dimuat.");
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    load();
  }, [load]);
  return { categories, loading, error, retry: load };
}
