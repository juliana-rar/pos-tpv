using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(bool onlyVisible = false, CancellationToken ct = default);
    Task<CategoryFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(CategoryFormDto form, CancellationToken ct = default);
    Task UpdateAsync(CategoryFormDto form, CancellationToken ct = default);
    Task SetVisibilityAsync(int id, bool visible, CancellationToken ct = default);

    /// <summary>Swap a category's display order with its neighbour. direction: -1 = up, +1 = down.</summary>
    Task MoveAsync(int id, int direction, CancellationToken ct = default);

    /// <summary>Delete a category. Throws <see cref="InvalidOperationException"/> if it still holds products.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<CategoryDto>> GetAllAsync(bool onlyVisible = false, CancellationToken ct = default)
    {
        var query = _uow.Repository<Category>().Query();
        if (onlyVisible) query = query.Where(c => c.IsVisible);
        // Projected so the product count is a SQL COUNT rather than loading every product.
        return await query
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Icon, c.Color, c.ImageUrl, c.DisplayOrder, c.IsVisible, c.Products.Count))
            .ToListAsync(ct);
    }

    public async Task<CategoryFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Category>().GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<CategoryFormDto>(entity);
    }

    public async Task<int> CreateAsync(CategoryFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Category>(form);
        entity.Id = 0;
        await _uow.Repository<Category>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(CategoryFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Category>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"Category {form.Id} not found.");
        _mapper.Map(form, entity);
        _uow.Repository<Category>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetVisibilityAsync(int id, bool visible, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Category>().GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.IsVisible = visible;
        _uow.Repository<Category>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task MoveAsync(int id, int direction, CancellationToken ct = default)
    {
        if (direction == 0) return;
        var repo = _uow.Repository<Category>();
        var ordered = await repo.Query().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);

        var index = ordered.FindIndex(c => c.Id == id);
        var target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= ordered.Count) return;

        // Reindex sequentially first so the swap moves the row even when orders collide (e.g. all 0).
        for (var i = 0; i < ordered.Count; i++) ordered[i].DisplayOrder = i;
        (ordered[index].DisplayOrder, ordered[target].DisplayOrder) = (target, index);
        repo.Update(ordered[index]);
        repo.Update(ordered[target]);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Category>();
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null) return;

        var hasProducts = await _uow.Repository<Product>().Query().AnyAsync(p => p.CategoryId == id, ct);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete a category that still has products.");

        repo.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }
}
