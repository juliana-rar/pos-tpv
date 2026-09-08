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
    Task<List<StockMovementDto>> GetAllMovementsAsync(CancellationToken ct = default);
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
        var lastUpdates = await _uow.Repository<StockMovement>().QueryNoTracking()
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, LastUpdatedAt = g.Max(m => m.CreatedAt) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastUpdatedAt, ct);

        var products = await _uow.Repository<Product>().QueryNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.CategoryId, CategoryName = p.Category.Name, p.StockQuantity, p.Price })
            .ToListAsync(ct);

        return products
            .Select(p => new StockItemDto(
                p.Id, p.Name, p.CategoryId, p.CategoryName, p.StockQuantity, p.Price,
                lastUpdates.TryGetValue(p.Id, out var d) ? d : null))
            .ToList();
    }

    public async Task<List<StockMovementDto>> GetAllMovementsAsync(CancellationToken ct = default)
    {
        return await _uow.Repository<StockMovement>().QueryNoTracking()
            .Include(m => m.Product).ThenInclude(p => p.Category)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto(
                m.Id, m.CreatedAt, m.ProductId, m.Product.Name, m.Product.CategoryId, m.Product.Category.Name,
                m.QuantityChange, m.Reason, m.Note))
            .ToListAsync(ct);
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
