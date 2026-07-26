using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IAllergenService
{
    Task<List<AllergenDto>> GetAllAsync(CancellationToken ct = default);
    Task<AllergenFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(AllergenFormDto form, CancellationToken ct = default);
    Task UpdateAsync(AllergenFormDto form, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class AllergenService : IAllergenService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AllergenService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<AllergenDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Allergen>().QueryNoTracking().OrderBy(a => a.Name).ToListAsync(ct);
        return _mapper.Map<List<AllergenDto>>(list);
    }

    public async Task<AllergenFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Allergen>().GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<AllergenFormDto>(entity);
    }

    public async Task<int> CreateAsync(AllergenFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Allergen>(form);
        entity.Id = 0;
        await _uow.Repository<Allergen>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(AllergenFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Allergen>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"Allergen {form.Id} not found.");
        _mapper.Map(form, entity);
        _uow.Repository<Allergen>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Allergen>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Allergen>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }
}
