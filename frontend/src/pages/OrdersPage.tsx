import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { Card, Button, Select, Textarea } from "../components/ui";
import { ResourceState } from "../components/ResourceState";
import { useResource } from "../hooks/useResource";
import { useAuth } from "../context/AuthContext";
import {
  marketplaceApi,
  money,
  priceUnit,
  type Order,
} from "../api/marketplaceApi";
export const orderLabels = {
  Pending: "Menunggu konfirmasi",
  Confirmed: "Dikonfirmasi",
  Completed: "Selesai",
  Cancelled: "Dibatalkan",
};
function CustomerOrders() {
  const result = useResource(marketplaceApi.orders, "orders");
  return (
    <>
      <ResourceState {...result} />
      {result.data?.length === 0 && (
        <Card>
          <h2>Belum ada pesanan</h2>
          <p>Temukan penyedia yang sesuai untuk kebutuhan Anda.</p>
          <Link className="btn btn-primary" to="/search">
            Cari penyedia
          </Link>
        </Card>
      )}
      {result.data?.map((o) => (
        <OrderCard key={o.id} order={o} reload={result.reload} />
      ))}
    </>
  );
}
function OrderCard({
  order: o,
  reload,
}: {
  order: Order;
  reload: () => unknown;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function review(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    setBusy(true);
    try {
      await marketplaceApi.review(o.id, {
        rating: Number(data.get("rating")),
        comment: data.get("comment"),
      });
      reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Ulasan gagal.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <Card className="order-card">
      <h2>{o.providerName}</h2>
      <p>
        {orderLabels[o.status]} · {o.scheduledDate}
      </p>
      <strong>
        {money(o.price)} / {priceUnit(o.pricingType)}
      </strong>
      <p>{o.addressDetail}</p>
      {o.status === "Completed" && !o.reviewed && (
        <form onSubmit={review}>
          <fieldset disabled={busy}>
            <Select name="rating" label="Penilaian">
              {[5, 4, 3, 2, 1].map((r) => (
                <option key={r} value={r}>
                  {r} / 5
                </option>
              ))}
            </Select>
            <Textarea
              name="comment"
              label="Pengalaman Anda"
              required
              minLength={3}
              maxLength={2000}
            />
            {error && <p role="alert">{error}</p>}
            <Button type="submit">Kirim ulasan</Button>
          </fieldset>
        </form>
      )}
      {o.reviewed && <p>Terima kasih, ulasan Anda telah tersimpan.</p>}
    </Card>
  );
}
export function OrdersPage() {
  const { session, loading } = useAuth();
  return (
    <AppShell>
      <h1>Pesanan saya</h1>
      {loading ? (
        <p>Memeriksa sesi…</p>
      ) : session?.user.role === "Customer" ? (
        <CustomerOrders />
      ) : (
        <Card>
          <h2>Semua pesanan, di satu tempat</h2>
          <p>Masuk sebagai Customer untuk melihat riwayat pesanan Anda.</p>
          <Link className="btn btn-primary" to="/login?returnTo=%2Forders">
            Masuk
          </Link>
          <Link className="text-link" to="/search">
            Jelajahi layanan
          </Link>
        </Card>
      )}
    </AppShell>
  );
}
