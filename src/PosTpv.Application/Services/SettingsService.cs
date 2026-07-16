using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface ISettingsService
{
    Task<AppSettingsDto> GetAsync(CancellationToken ct = default);
    Task<AppSettingsFormDto> GetForEditAsync(CancellationToken ct = default);
    Task UpdateAsync(AppSettingsFormDto form, CancellationToken ct = default);
}

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly AppSettingsCache _cache;

    public SettingsService(IUnitOfWork uow, IMapper mapper, AppSettingsCache cache)
    {
        _uow = uow;
        _mapper = mapper;
        _cache = cache;
    }

    // The table always holds exactly one row; create it lazily so a fresh database (or one
    // that predates this feature) still works without a manual migration-time data fix.
    private async Task<AppSetting> GetEntityAsync(CancellationToken ct)
    {
        var entity = await _uow.Repository<AppSetting>().Query().FirstOrDefaultAsync(ct);
        if (entity is null)
        {
            entity = new AppSetting();
            await _uow.Repository<AppSetting>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return entity;
    }

    public async Task<AppSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var dto = _mapper.Map<AppSettingsDto>(await GetEntityAsync(ct));
        _cache.Set(dto);
        return dto;
    }

    public async Task<AppSettingsFormDto> GetForEditAsync(CancellationToken ct = default) =>
        _mapper.Map<AppSettingsFormDto>(await GetEntityAsync(ct));

    public async Task UpdateAsync(AppSettingsFormDto form, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(ct);
        _mapper.Map(form, entity);
        _uow.Repository<AppSetting>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        _cache.Set(_mapper.Map<AppSettingsDto>(entity));
    }
}
