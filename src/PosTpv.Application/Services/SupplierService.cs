using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface ISupplierService
{
    Task<List<SupplierDto>> GetAllAsync(CancellationToken ct = default);
    Task<SupplierFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(SupplierFormDto form, CancellationToken ct = default);
    Task UpdateAsync(SupplierFormDto form, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<List<SupplierDocumentDto>> GetDocumentsAsync(int supplierId, CancellationToken ct = default);
    Task<int> AddDocumentAsync(int supplierId, string fileName, string fileUrl, string? contentType, long fileSize, CancellationToken ct = default);
    Task DeleteDocumentAsync(int documentId, CancellationToken ct = default);
}

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SupplierService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<SupplierDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Supplier>().QueryNoTracking()
            .Include(s => s.Documents)
            .Include(s => s.Purchases)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        return _mapper.Map<List<SupplierDto>>(list);
    }

    public async Task<SupplierFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Supplier>().GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<SupplierFormDto>(entity);
    }

    public async Task<int> CreateAsync(SupplierFormDto form, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Supplier>(form);
        entity.Id = 0;
        await _uow.Repository<Supplier>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(SupplierFormDto form, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Supplier>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"Supplier {form.Id} not found.");
        _mapper.Map(form, entity);
        _uow.Repository<Supplier>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var hasPurchases = await _uow.Repository<Purchase>().QueryNoTracking().AnyAsync(p => p.SupplierId == id, ct);
        if (hasPurchases)
            throw new InvalidOperationException("This supplier has purchases on record and cannot be deleted.");

        var entity = await _uow.Repository<Supplier>().GetByIdAsync(id, ct);
        if (entity is null) return;
        _uow.Repository<Supplier>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<SupplierDocumentDto>> GetDocumentsAsync(int supplierId, CancellationToken ct = default)
    {
        var list = await _uow.Repository<SupplierDocument>().QueryNoTracking()
            .Where(d => d.SupplierId == supplierId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
        return _mapper.Map<List<SupplierDocumentDto>>(list);
    }

    public async Task<int> AddDocumentAsync(int supplierId, string fileName, string fileUrl, string? contentType, long fileSize, CancellationToken ct = default)
    {
        var doc = new SupplierDocument
        {
            SupplierId = supplierId,
            FileName = fileName,
            FileUrl = fileUrl,
            ContentType = contentType,
            FileSize = fileSize
        };
        await _uow.Repository<SupplierDocument>().AddAsync(doc, ct);
        await _uow.SaveChangesAsync(ct);
        return doc.Id;
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<SupplierDocument>().GetByIdAsync(documentId, ct);
        if (entity is null) return;
        _uow.Repository<SupplierDocument>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }
}
