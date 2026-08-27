using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IStockService
{
    Task<List<StockItemDto>> GetAllAsync(CancellationToken ct = default);
    Task AdjustAsync(StockAdjustFormDto form, CancellationToken ct = default);
}

public class StockService : IStockService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public StockService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<StockItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Product>().QueryNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Select(p => new StockItemDto(p.Id, p.Name, p.Category.Name, p.StockQuantity))
            .ToListAsync(ct);
        return list;
    }

    public async Task AdjustAsync(StockAdjustFormDto form, CancellationToken ct = default)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(form.ProductId, ct)
            ?? throw new KeyNotFoundException($"Product {form.ProductId} not found.");

        var delta = form.NewQuantity - product.StockQuantity;
        if (delta == 0) return;

        product.StockQuantity = form.NewQuantity;
        _uow.Repository<Product>().Update(product);

        await _uow.Repository<StockMovement>().AddAsync(new StockMovement
        {
            ProductId = form.ProductId,
            QuantityChange = delta,
            Reason = StockMovementReason.Adjustment,
            Note = form.Note
        }, ct);

        await _uow.SaveChangesAsync(ct);
    }
}
