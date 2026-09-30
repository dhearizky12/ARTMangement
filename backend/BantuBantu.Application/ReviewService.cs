using BantuBantu.Domain;

namespace BantuBantu.Application;

public class ReviewService(IMarketplaceRepository repo, ICurrentActor current) : ApplicationService(current)
{
    private static ReviewAdminDto Map(Review review) => new(
        review.Id,
        review.OrderId,
        review.ProviderId,
        review.Provider.FullName,
        review.CustomerId,
        review.Customer.FullName,
        review.Rating,
        review.Comment,
        review.CreatedAt,
        review.IsHidden,
        review.HiddenReason,
        review.HiddenAt);

    public async Task<ReviewAdminDto[]> List(CancellationToken ct) =>
        (await repo.AdminReviewsAsync(Admin(), ct)).Select(Map).ToArray();

    public async Task<ReviewAdminDto> Hide(Guid id, ReviewModerationRequest request, CancellationToken ct)
    {
        var actor = Admin();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ProfileException("Alasan menyembunyikan ulasan wajib diisi.");
        var review = await repo.AdminReviewAsync(actor, id, ct) ?? throw new ProfileException("Ulasan tidak ditemukan.", 404);
        if (review.IsHidden) throw new ProfileException("Ulasan sudah disembunyikan.", 409);
        review.IsHidden = true;
        review.HiddenReason = request.Reason.Trim();
        review.HiddenAt = DateTimeOffset.UtcNow;
        review.HiddenBy = actor.Id;
        repo.Audit(actor, "review.hide", "Review", id, review.HiddenReason);
        await repo.SaveAsync(ct);
        return Map(review);
    }

    public async Task<ReviewAdminDto> Restore(Guid id, CancellationToken ct)
    {
        var actor = Admin();
        var review = await repo.AdminReviewAsync(actor, id, ct) ?? throw new ProfileException("Ulasan tidak ditemukan.", 404);
        if (!review.IsHidden) throw new ProfileException("Ulasan sudah terlihat.", 409);
        review.IsHidden = false;
        review.HiddenReason = null;
        review.HiddenAt = null;
        review.HiddenBy = null;
        repo.Audit(actor, "review.restore", "Review", id);
        await repo.SaveAsync(ct);
        return Map(review);
    }
}
