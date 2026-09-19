import { useState, type FormEvent } from "react";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card, Input, Select, Button } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import { adminApi } from "../../api/marketplaceApi";
import { authApi } from "../../api/authApi";
export function AgencyPage() {
  const result = useResource(adminApi.agencies, "agencies");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  async function act(path: string, body: unknown) {
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await authApi.post(path, body);
      await result.reload();
      setMessage("Data tersimpan.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan.");
    } finally {
      setBusy(false);
    }
  }
  function create(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    void act("/api/admin/agencies", {
      name: d.get("name"),
      contactInfo: d.get("contactInfo"),
    });
  }
  return (
    <AdminLayout>
      <h1>Agency & administrator</h1>
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      <div className="admin-grid">
        <Card>
          <h2>Daftarkan agency</h2>
          <form onSubmit={create}>
            <fieldset disabled={busy}>
              <Input
                label="Nama agency"
                name="name"
                minLength={2}
                maxLength={120}
                required
              />
              <Input
                label="Kontak"
                name="contactInfo"
                maxLength={500}
                required
              />
              <Button type="submit">Buat agency pending</Button>
            </fieldset>
          </form>
        </Card>
        <Card>
          <h2>Akun Agency Admin</h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void act("/api/admin/accounts", Object.fromEntries(d));
            }}
          >
            <fieldset disabled={busy}>
              <Input
                label="Nama admin"
                name="fullName"
                maxLength={120}
                required
              />
              <Input label="Email admin" name="email" type="email" required />
              <Input
                label="Kata sandi awal"
                name="password"
                type="password"
                minLength={14}
                maxLength={256}
                autoComplete="new-password"
                required
              />
              <Select name="agencyId" label="Agency disetujui" required>
                <option value="">Pilih agency</option>
                {result.data
                  ?.filter((a) => a.status === "Approved")
                  .map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
              </Select>
              <Button type="submit">Buat akun admin</Button>
            </fieldset>
          </form>
        </Card>
      </div>
      <ResourceState {...result} />
      {result.data?.map((a) => (
        <Card key={a.id}>
          <h2>{a.name}</h2>
          <p>{a.contactInfo}</p>
          <p>{a.status}</p>
          <div className="filter-chips">
            {(["Pending", "Approved", "Suspended"] as const).map((status) => (
              <Button
                key={status}
                variant="ghost"
                disabled={busy || a.status === status}
                onClick={() =>
                  act(`/api/admin/agencies/${a.id}/status`, { status })
                }
              >
                {status}
              </Button>
            ))}
          </div>
        </Card>
      ))}
    </AdminLayout>
  );
}
