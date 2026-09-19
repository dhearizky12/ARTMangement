import { useState } from "react";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card, Button } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import { adminApi, money } from "../../api/marketplaceApi";
import { authApi } from "../../api/authApi";
import { orderLabels } from "../OrdersPage";
export function AdminOrdersPage() {
  const result = useResource(adminApi.orders, "admin-orders");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <AdminLayout>
      <h1>Pesanan layanan</h1>
      <ResourceState {...result} />
      {error && <p role="alert">{error}</p>}
      {result.data?.length === 0 && <p>Belum ada pesanan.</p>}
      {result.data?.map((o) => (
        <Card key={o.id}>
          <h2>{o.providerName}</h2>
          <p>
            {o.scheduledDate} · {orderLabels[o.status]}
          </p>
          <p>{money(o.price)}</p>
          <p>{o.addressDetail}</p>
          <div className="filter-chips">
            {(o.status === "Pending"
              ? ["Confirmed", "Cancelled"]
              : o.status === "Confirmed"
                ? ["Completed", "Cancelled"]
                : []
            ).map((status) => (
              <Button
                key={status}
                disabled={busy}
                variant="secondary"
                onClick={async () => {
                  setBusy(true);
                  setError("");
                  try {
                    await authApi.post(`/api/admin/orders/${o.id}/status`, {
                      status,
                    });
                    await result.reload();
                  } catch (e) {
                    setError(
                      e instanceof Error
                        ? e.message
                        : "Gagal memperbarui pesanan.",
                    );
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {orderLabels[status as keyof typeof orderLabels]}
              </Button>
            ))}
          </div>
        </Card>
      ))}
    </AdminLayout>
  );
}
