using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<ProductDto>> GetByCategoryAsync(int categoryId, bool onlyAvailable = false, bool includeHidden = false, CancellationToken ct = default);
    Task<List<ExtraDto>> GetExtrasAsync(int productId, CancellationToken ct = default);
    Task<ProductFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ProductFormDto form, CancellationToken ct = default);
    Task UpdateAsync(ProductFormDto form, CancellationToken ct = default);
    Task SetAvailabilityAsync(int id, bool available, CancellationToken ct = default);

    /// <summary>Clone a product (and its extras) into the same category, appended at the end.</summary>
    Task<int> DuplicateAsync(int id, CancellationToken ct = default);

    /// <summary>Swap a product's order with its neighbour inside its category. direction: -1 = up, +1 = down.</summary>
    Task MoveAsync(int id, int direction, CancellationToken ct = default);

    /// <summary>Move a product to another category, appended at the end of the target.</summary>
    Task SetCategoryAsync(int id, int categoryId, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Product>().Query()
            .Include(p => p.Category)
            .Include(p => p.Extras)
            .Include(p => p.Allergens)
            .OrderBy(p => p.Category.DisplayOrder).ThenBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);
        return _mapper.Map<List<ProductDto>>(list);
    }

    public async Task<List<ProductDto>> GetByCategoryAsync(int categoryId, bool onlyAvailable = false, bool includeHidden = false, CancellationToken ct = default)
    {
        var query = _uow.Repository<Product>().Query()
            .Include(p => p.Category)
            .Include(p => p.Extras)
            .Include(p => p.Allergens)
            .Where(p => p.CategoryId == categoryId);
        if (!includeHidden) query = query.Where(p => p.IsVisible);
        if (onlyAvailable) query = query.Where(p => p.IsAvailable);
        var list = await query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return _mapper.Map<List<ProductDto>>(list);
    }

    public async Task<List<ExtraDto>> GetExtrasAsync(int productId, CancellationToken ct = default)
    {
        var product = await _uow.Repository<Product>().Query()
            .Include(p => p.Extras)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        return product?.Extras
            .OrderBy(e => e.Name)
            .Select(e => new ExtraDto(e.Id, e.Name, e.Price))
            .ToList() ?? new();
    }

    public async Task<ProductFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Product>().Query()
            .Include(p => p.Allergens)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity is null ? null : _mapper.Map<ProductFormDto>(entity);
    }

    public async Task<int> CreateAsync(ProductFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Product>(form);
        entity.Id = 0;
        entity.Allergens = await ResolveAllergensAsync(form.AllergenIds, ct);
        await _uow.Repository<Product>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(ProductFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Product>().Query()
            .Include(p => p.Allergens)
            .FirstOrDefaultAsync(p => p.Id == form.Id, ct)
            ?? throw new KeyNotFoundException($"Product {form.Id} not found.");
        _mapper.Map(form, entity);
        entity.Allergens = await ResolveAllergensAsync(form.AllergenIds, ct);
        _uow.Repository<Product>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<List<Allergen>> ResolveAllergensAsync(IEnumerable<int> allergenIds, CancellationToken ct)
    {
        var ids = allergenIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Allergen>();
        return await _uow.Repository<Allergen>().Query().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
    }

    public async Task SetAvailabilityAsync(int id, bool available, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Product>().GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.IsAvailable = available;
        _uow.Repository<Product>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateAsync(int id, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Product>();
        var source = await repo.Query().Include(p => p.Extras).Include(p => p.Allergens).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Product {id} not found.");

        var nextOrder = await repo.Query()
            .Where(p => p.CategoryId == source.CategoryId)
            .Select(p => (int?)p.DisplayOrder).MaxAsync(ct) ?? -1;

        var clone = new Product
        {
            Name = $"{source.Name} (copy)",
            Description = source.Description,
            Price = source.Price,
            VatRate = source.VatRate,
            Color = source.Color,
            ImageUrl = source.ImageUrl,
            DisplayOrder = nextOrder + 1,
            IsVisible = source.IsVisible,
            IsAvailable = source.IsAvailable,
            PreparationMinutes = source.PreparationMinutes,
            Ingredients = source.Ingredients,
            CategoryId = source.CategoryId,
            // Extras/Allergens are shared (many-to-many): link the same rows, don't clone them.
            Extras = source.Extras.ToList(),
            Allergens = source.Allergens.ToList(),
        };
        await repo.AddAsync(clone, ct);
        await _uow.SaveChangesAsync(ct);
        return clone.Id;
    }

    public async Task MoveAsync(int id, int direction, CancellationToken ct = default)
    {
        if (direction == 0) return;
        var repo = _uow.Repository<Product>();
        var target = await repo.GetByIdAsync(id, ct);
        if (target is null) return;

        var siblings = await repo.Query()
            .Where(p => p.CategoryId == target.CategoryId)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);

        var index = siblings.FindIndex(p => p.Id == id);
        var swapWith = index + Math.Sign(direction);
        if (index < 0 || swapWith < 0 || swapWith >= siblings.Count) return;

        // Reindex sequentially first so the swap moves the row even when orders collide (e.g. all 0).
        for (var i = 0; i < siblings.Count; i++) siblings[i].DisplayOrder = i;
        (siblings[index].DisplayOrder, siblings[swapWith].DisplayOrder) = (swapWith, index);
        repo.Update(siblings[index]);
        repo.Update(siblings[swapWith]);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetCategoryAsync(int id, int categoryId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Product>();
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null || entity.CategoryId == categoryId) return;

        var nextOrder = await repo.Query()
            .Where(p => p.CategoryId == categoryId)
            .Select(p => (int?)p.DisplayOrder).MaxAsync(ct) ?? -1;

        entity.CategoryId = categoryId;
        entity.DisplayOrder = nextOrder + 1;
        repo.Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Product>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Product>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }
}
