import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import { authApi } from "../../api/authApi";
interface Entry {
  id: string;
  actorId: string;
  providerId: string | null;
  action: string;
  detail: string;
  createdAt: string;
}
export function AuditPage() {
  const result = useResource(
    () => authApi.get<Entry[]>("/api/admin/audit"),
    "audit",
  );
  return (
    <AdminLayout>
      <h1>Riwayat tindakan admin</h1>
      <p>200 tindakan terbaru.</p>
      <ResourceState {...result} />
      {result.data?.map((e) => (
        <Card key={e.id}>
          <h2>{e.action}</h2>
          <p>{e.detail}</p>
          <p className="wrap">Aktor: {e.actorId}</p>
          {e.providerId && <p className="wrap">Penyedia: {e.providerId}</p>}
          <small>{new Date(e.createdAt).toLocaleString("id-ID")}</small>
        </Card>
      ))}
    </AdminLayout>
  );
}
