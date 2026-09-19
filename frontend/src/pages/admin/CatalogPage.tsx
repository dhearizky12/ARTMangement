import { useState, type FormEvent } from "react";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Card, Input, Textarea, Select, Button } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import {
  adminApi,
  marketplaceApi,
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
          <label className="check-option">
            <input
              type="checkbox"
              name="isActive"
              defaultChecked={category?.isActive ?? true}
            />
            Aktif
          </label>
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
            <Button
              variant="ghost"
              onClick={async () => {
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
            await authApi.mutate(`/api/admin/content/${d.get("id")}`, "PUT", {
              title: d.get("title"),
              body: d.get("body"),
              sortOrder: Number(d.get("sortOrder")),
            });
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
          <Button type="submit">Simpan konten</Button>
          {error && <p role="alert">{error}</p>}
        </fieldset>
      </form>
    </Card>
  );
}
export function CatalogPage() {
  const categories = useResource(adminApi.categories, "admin-categories");
  const content = useResource(marketplaceApi.content, "admin-content");
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
