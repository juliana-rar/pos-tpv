using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IPurchaseService
{
    Task<List<PurchaseDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CreateAsync(PurchaseFormDto form, CancellationToken ct = default);
}

public class PurchaseService : IPurchaseService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public PurchaseService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<PurchaseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Purchase>().QueryNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Lines).ThenInclude(l => l.Product)
            .OrderByDescending(p => p.Date)
            .ToListAsync(ct);
        return _mapper.Map<List<PurchaseDto>>(list);
    }

    /// <summary>Records a purchase and immediately restocks every line's product, logging a
    /// StockMovement per line so the change is auditable alongside sale/adjustment movements.</summary>
    public Task<int> CreateAsync(PurchaseFormDto form, CancellationToken ct = default) =>
        _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var purchase = new Purchase
            {
                SupplierId = form.SupplierId,
                Date = form.Date,
                Reference = form.Reference,
                Notes = form.Notes
            };
            await _uow.Repository<Purchase>().AddAsync(purchase, innerCt);
            await _uow.SaveChangesAsync(innerCt);

            foreach (var line in form.Lines)
            {
                await _uow.Repository<PurchaseLine>().AddAsync(new PurchaseLine
                {
                    PurchaseId = purchase.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost
                }, innerCt);

                var product = await _uow.Repository<Product>().GetByIdAsync(line.ProductId, innerCt)
                    ?? throw new KeyNotFoundException($"Product {line.ProductId} not found.");
                product.StockQuantity += line.Quantity;
                _uow.Repository<Product>().Update(product);

                await _uow.Repository<StockMovement>().AddAsync(new StockMovement
                {
                    ProductId = line.ProductId,
                    QuantityChange = line.Quantity,
                    Reason = StockMovementReason.Purchase,
                    Note = purchase.Reference
                }, innerCt);
            }

            await _uow.SaveChangesAsync(innerCt);
            return purchase.Id;
        }, ct);
}
