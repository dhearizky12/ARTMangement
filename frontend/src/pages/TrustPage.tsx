import { AppShell } from "../components/AppShell";
import { Card } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { marketplaceApi } from "../api/marketplaceApi";
export function TrustPage() {
  const result = useResource(marketplaceApi.content, "trust");
  return (
    <AppShell>
      <h1>Kepercayaan dimulai dari kejelasan.</h1>
      <ResourceState {...result} />
      {result.data?.map((c) => (
        <Card key={c.id}>
          <h2>{c.title}</h2>
          <p className="preserve-lines">{c.body}</p>
        </Card>
      ))}
    </AppShell>
  );
}
