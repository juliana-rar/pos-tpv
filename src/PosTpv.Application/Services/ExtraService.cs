using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IExtraService
{
    Task<List<ExtraDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExtraFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ExtraFormDto form, CancellationToken ct = default);
    Task UpdateAsync(ExtraFormDto form, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ExtraService : IExtraService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ExtraService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<ExtraDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Extra>().QueryNoTracking().OrderBy(e => e.Name).ToListAsync(ct);
        return _mapper.Map<List<ExtraDto>>(list);
    }

    public async Task<ExtraFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Extra>().QueryNoTracking()
            .Include(e => e.Products)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        return entity is null ? null : _mapper.Map<ExtraFormDto>(entity);
    }

    public async Task<int> CreateAsync(ExtraFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Extra>(form);
        entity.Id = 0;
        entity.Products = await ResolveProductsAsync(form.ProductIds, ct);
        await _uow.Repository<Extra>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(ExtraFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Extra>().Query()
            .Include(e => e.Products)
            .FirstOrDefaultAsync(e => e.Id == form.Id, ct)
            ?? throw new KeyNotFoundException($"Extra {form.Id} not found.");
        _mapper.Map(form, entity);
        entity.Products = await ResolveProductsAsync(form.ProductIds, ct);
        _uow.Repository<Extra>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<List<Product>> ResolveProductsAsync(IEnumerable<int> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Product>();
        return await _uow.Repository<Product>().Query().Where(p => ids.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Extra>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Extra>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }
}
