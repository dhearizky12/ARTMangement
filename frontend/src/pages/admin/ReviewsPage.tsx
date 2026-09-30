import { useState } from "react";
import { AdminLayout } from "../../components/admin/AdminLayout";
import { Badge, Button, Card, Textarea } from "../../components/ui";
import { ResourceState } from "../../components/ResourceState";
import { useResource } from "../../hooks/useResource";
import { adminApi, type ReviewAdmin } from "../../api/marketplaceApi";

function ReviewCard({ review, reload }: { review: ReviewAdmin; reload: () => unknown }) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function moderate(action: "hide" | "restore") {
    if (action === "hide" && !reason.trim()) {
      setError("Alasan wajib diisi.");
      return;
    }
    if (!window.confirm(action === "hide" ? "Sembunyikan ulasan ini?" : "Tampilkan kembali ulasan ini?")) return;
    setBusy(true);
    setError("");
    try {
      if (action === "hide") await adminApi.hideReview(review.id, reason.trim());
      else await adminApi.restoreReview(review.id);
      await reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memoderasi ulasan.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <Card>
      <div className="section-title">
        <div>
          <h2>{review.providerName}</h2>
          <p className="muted">Dari {review.reviewerName} · {new Date(review.createdAt).toLocaleDateString("id-ID")}</p>
        </div>
        <Badge tone={review.isHidden ? "neutral" : "success"}>
          {review.isHidden ? "Disembunyikan" : "Tampil"}
        </Badge>
      </div>
      <p aria-label={`Rating ${review.rating} dari 5`}>Rating: {"★".repeat(review.rating)}{"☆".repeat(5 - review.rating)}</p>
      <p className="preserve-lines">{review.comment}</p>
      {review.isHidden && <p className="muted preserve-lines">Alasan: {review.hiddenReason || "—"}</p>}
      {!review.isHidden && (
        <Textarea label="Alasan menyembunyikan" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
      )}
      <div className="filter-chips">
        <Button variant={review.isHidden ? "secondary" : "ghost"} disabled={busy} onClick={() => void moderate(review.isHidden ? "restore" : "hide")}>
          {review.isHidden ? "Tampilkan kembali" : "Sembunyikan"}
        </Button>
      </div>
      {error && <p role="alert">{error}</p>}
    </Card>
  );
}

export function ReviewsPage() {
  const result = useResource(adminApi.reviews, "admin-reviews");
  return (
    <AdminLayout>
      <h1>Moderasi ulasan</h1>
      <p className="muted">Ulasan yang disembunyikan tidak memengaruhi rating publik.</p>
      <ResourceState {...result} />
      {result.data?.length === 0 && <p>Belum ada ulasan.</p>}
      {result.data?.map((review) => <ReviewCard key={review.id} review={review} reload={result.reload} />)}
    </AdminLayout>
  );
}
