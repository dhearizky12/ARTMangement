import type { ReactNode } from "react";
import { Button, Spinner } from "./ui";
export function ResourceState({
  loading,
  error,
  reload,
  skeleton,
}: {
  loading: boolean;
  error: string;
  reload: () => unknown;
  skeleton?: ReactNode;
}) {
  return loading ? (
    (skeleton ?? (
      <p role="status" className="loading">
        <Spinner /> Memuat…
      </p>
    ))
  ) : error ? (
    <div role="alert">
      <p>{error}</p>
      <Button onClick={() => reload()}>Coba lagi</Button>
    </div>
  ) : null;
}
