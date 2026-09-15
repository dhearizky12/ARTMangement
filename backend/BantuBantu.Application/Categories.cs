using BantuBantu.Domain;
namespace BantuBantu.Application;

public record ServiceCategoryDto(Guid Id, string Slug, string Name, string Description, string IconKey, bool IsFeatured);
public interface ICategoryRepository { Task<IReadOnlyList<ServiceCategory>> ListActiveAsync(CancellationToken ct); }
public interface ICategoryService { Task<IReadOnlyList<ServiceCategoryDto>> ListAsync(CancellationToken ct); }
public class CategoryService(ICategoryRepository repository) : ICategoryService
{
    public async Task<IReadOnlyList<ServiceCategoryDto>> ListAsync(CancellationToken ct) =>
        (await repository.ListActiveAsync(ct)).Select(x => new ServiceCategoryDto(x.Id, x.Slug, x.Name, x.Description, x.IconKey, x.IsFeatured)).ToList();
}
