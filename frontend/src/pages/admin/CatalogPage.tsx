import { useState, type FormEvent } from "react";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card, Input, Textarea, Select, Button } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import {
  adminApi,
  type Category,
  type Content,
} from "../../api/marketplaceApi";
import { authApi } from "../../api/authApi";
function CategoryEditor({
  category,
  reload,
}: {
  category?: Category;
  reload: () => unknown;
}) {
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    const body = {
      slug: d.get("slug"),
      name: d.get("name"),
      description: d.get("description"),
      iconKey: d.get("iconKey"),
      sortOrder: Number(d.get("sortOrder")),
      isActive: d.has("isActive"),
      isFeatured: d.has("isFeatured"),
    };
    setBusy(true);
    setError("");
    try {
      if (category)
        await authApi.mutate(
          `/api/admin/categories/${category.id}`,
          "PUT",
          body,
        );
      else await authApi.post("/api/admin/categories", body);
      reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <Card>
      <h2>{category?.name || "Kategori baru"}</h2>
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <Input
            label="Nama kategori"
            name="name"
            defaultValue={category?.name}
            required
            maxLength={120}
          />
          <Input
            label="Slug"
            name="slug"
            defaultValue={category?.slug}
            required
            pattern="[a-z0-9]+(-[a-z0-9]+)*"
            maxLength={80}
          />
          <Textarea
            label="Deskripsi"
            name="description"
            defaultValue={category?.description}
            required
            maxLength={500}
          />
          <Select
            label="Ikon"
            name="iconKey"
            defaultValue={category?.iconKey || "briefcase"}
          >
            {["briefcase", "house", "car", "sparkles", "sprout"].map((i) => (
              <option key={i}>{i}</option>
            ))}
          </Select>
          <Input
            label="Urutan"
            name="sortOrder"
            type="number"
            defaultValue={category?.sortOrder || 0}
          />
          {category ? (
            <p className="muted">Status: {category.isActive ? "Aktif" : "Nonaktif"}. Gunakan tombol status dengan konfirmasi di bawah.</p>
          ) : (
            <label className="check-option">
              <input type="checkbox" name="isActive" defaultChecked />
              Aktif
            </label>
          )}
          <label className="check-option">
            <input
              type="checkbox"
              name="isFeatured"
              defaultChecked={category?.isFeatured ?? false}
            />
            Unggulan
          </label>
          <Button type="submit">Simpan kategori</Button>
          {category && (
            <>
              <Button
                variant="secondary"
                onClick={async () => {
                  if (!window.confirm(category.isActive
                    ? `Nonaktifkan kategori ${category.name}? Provider yang sudah memakai kategori ini tetap terlihat di marketplace.`
                    : `Aktifkan kembali kategori ${category.name}?`)) return;
                  setBusy(true);
                  setError("");
                  try {
                    await authApi.mutate(`/api/admin/categories/${category.id}`, "PUT", {
                      slug: category.slug,
                      name: category.name,
                      description: category.description,
                      iconKey: category.iconKey,
                      sortOrder: category.sortOrder,
                      isActive: !category.isActive,
                      isFeatured: category.isFeatured,
                    });
                    reload();
                  } catch (e) {
                    setError(e instanceof Error ? e.message : "Gagal memperbarui status.");
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {category.isActive ? "Nonaktifkan" : "Aktifkan"}
              </Button>
              <Button
                variant="ghost"
                onClick={async () => {
                  if (!window.confirm(`Hapus kategori ${category.name} secara permanen? Gunakan Nonaktifkan jika kategori pernah dipakai provider.`)) return;
                setBusy(true);
                try {
                  await authApi.mutate(
                    `/api/admin/categories/${category.id}`,
                    "DELETE",
                  );
                  reload();
                } catch (e) {
                  setError(e instanceof Error ? e.message : "Gagal menghapus.");
                } finally {
                  setBusy(false);
                }
                }}
              >
                Hapus kategori
              </Button>
            </>
          )}
          {error && <p role="alert">{error}</p>}
        </fieldset>
      </form>
    </Card>
  );
}
function ContentEditor({
  content,
  reload,
}: {
  content?: Content;
  reload: () => unknown;
}) {
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <Card>
      <h2>{content?.title || "Konten informasi baru"}</h2>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          setBusy(true);
          setError("");
          try {
            const body = {
              id: d.get("id"),
              title: d.get("title"),
              body: d.get("body"),
              iconName: d.get("iconName") || null,
              sortOrder: Number(d.get("sortOrder")),
              isActive: d.has("isActive"),
            };
            if (content) await adminApi.updateTrustSection(content.id, body);
            else await adminApi.createTrustSection(body);
            reload();
          } catch (e) {
            setError(e instanceof Error ? e.message : "Gagal menyimpan.");
          } finally {
            setBusy(false);
          }
        }}
      >
        <fieldset disabled={busy}>
          <Input
            label="Kode konten"
            name="id"
            required
            pattern="[a-z0-9-]+"
            maxLength={80}
            readOnly={!!content}
            defaultValue={content?.id}
          />
          <Input
            label="Judul"
            name="title"
            required
            maxLength={150}
            defaultValue={content?.title}
          />
          <Input
            label="Nama ikon (opsional)"
            name="iconName"
            maxLength={80}
            defaultValue={content?.iconName || ""}
          />
          <Textarea
            label="Isi konten"
            name="body"
            rows={6}
            required
            maxLength={5000}
            defaultValue={content?.body}
          />
          <Input
            label="Urutan"
            name="sortOrder"
            type="number"
            defaultValue={content?.sortOrder || 0}
          />
          <label className="check-option">
            <input type="checkbox" name="isActive" defaultChecked={content?.isActive ?? true} />
            Tampilkan di halaman publik
          </label>
          <Button type="submit">Simpan konten</Button>
          {content && (
            <>
              <Button
                variant="secondary"
                onClick={async () => {
                  const next = !content.isActive;
                  if (!window.confirm(next ? `Aktifkan ${content.title}?` : `Nonaktifkan ${content.title}?`)) return;
                  setBusy(true);
                  try {
                    await adminApi.updateTrustSection(content.id, {
                      id: content.id,
                      title: content.title,
                      body: content.body,
                      iconName: content.iconName,
                      sortOrder: content.sortOrder,
                      isActive: next,
                    });
                    reload();
                  } catch (e) {
                    setError(e instanceof Error ? e.message : "Gagal memperbarui status.");
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {content.isActive ? "Nonaktifkan" : "Aktifkan"}
              </Button>
              <Button
                variant="ghost"
                onClick={async () => {
                  if (!window.confirm(`Hapus blok ${content.title}?`)) return;
                  setBusy(true);
                  try {
                    await adminApi.deleteTrustSection(content.id);
                    reload();
                  } catch (e) {
                    setError(e instanceof Error ? e.message : "Gagal menghapus.");
                  } finally {
                    setBusy(false);
                  }
                }}
              >Hapus blok</Button>
            </>
          )}
          {error && <p role="alert">{error}</p>}
        </fieldset>
      </form>
    </Card>
  );
}
export function CatalogPage() {
  const categories = useResource(adminApi.categories, "admin-categories");
  const content = useResource(adminApi.trustSections, "admin-trust-sections");
  return (
    <AdminLayout>
      <h1>Kategori & informasi</h1>
      <ResourceState {...categories} />
      <details>
        <summary>Tambah kategori layanan</summary>
        <CategoryEditor reload={categories.reload} />
      </details>
      {categories.data?.map((c) => (
        <details key={c.id}>
          <summary>
            {c.name} · {c.isActive ? "Aktif" : "Nonaktif"}
          </summary>
          <CategoryEditor category={c} reload={categories.reload} />
        </details>
      ))}
      <h2>Halaman kepercayaan</h2>
      <ResourceState {...content} />
      <details>
        <summary>Tambah blok informasi</summary>
        <ContentEditor reload={content.reload} />
      </details>
      {content.data?.map((c) => (
        <details key={c.id}>
          <summary>{c.title}</summary>
          <ContentEditor content={c} reload={content.reload} />
        </details>
      ))}
    </AdminLayout>
  );
}
