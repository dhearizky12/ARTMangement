import { AppShell } from "../components/AppShell";
import { Card } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { marketplaceApi } from "../api/marketplaceApi";
export function TrustPage() {
  const result = useResource(marketplaceApi.trustSections, "trust");
  const sections = result.data || [];
  const intro = sections[0];
  return (
    <AppShell>
      <ResourceState {...result} />
      {intro && (
        <Card lifted>
          <p className="eyebrow">Keamanan layanan</p>
          <h1>{intro.title}</h1>
          <p className="preserve-lines">{intro.body}</p>
        </Card>
      )}
      {sections.slice(1).map((c, index) => (
        <Card key={c.id}>
          <p className="eyebrow">Langkah {index + 1}</p>
          <h2>{c.title}</h2>
          <p className="preserve-lines">{c.body}</p>
        </Card>
      ))}
    </AppShell>
  );
}
