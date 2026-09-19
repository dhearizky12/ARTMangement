import { Button } from "./ui";
export function ResourceState({
  loading,
  error,
  reload,
}: {
  loading: boolean;
  error: string;
  reload: () => unknown;
}) {
  return loading ? (
    <p role="status">Memuat…</p>
  ) : error ? (
    <div role="alert">
      <p>{error}</p>
      <Button onClick={() => reload()}>Coba lagi</Button>
    </div>
  ) : null;
}
