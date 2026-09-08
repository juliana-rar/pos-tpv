using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Application.Services;

public interface IAlbaranScanService
{
    Task<int> CreateAsync(string imageUrl, int? supplierId, CancellationToken ct = default);
    Task ProcessAsync(int scanId, string absoluteImagePath, CancellationToken ct = default);
    Task<List<AlbaranScanSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<AlbaranScanDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task SaveDraftAsync(int id, AlbaranScanEditDto dto, CancellationToken ct = default);
    Task<int> ValidateAndCreatePurchaseAsync(int id, AlbaranScanEditDto dto, CancellationToken ct = default);
    Task RetryAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Turns a photo of a supplier delivery note (albarán) into a real Purchase: an <see cref="IOllamaVisionClient"/>
/// call extracts proveedor/líneas/total, a person reviews/corrects it, and validating reuses
/// <see cref="IPurchaseService.CreateAsync"/> as-is so the stock/StockMovement logic isn't duplicated.
/// </summary>
public class AlbaranScanService : IAlbaranScanService
{
    private static readonly JsonSerializerOptions ExtractionJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IOllamaVisionClient _ollama;
    private readonly ISupplierNotifier _notifier;
    private readonly IPurchaseService _purchaseService;

    public AlbaranScanService(
        IUnitOfWork uow, IMapper mapper, IOllamaVisionClient ollama,
        ISupplierNotifier notifier, IPurchaseService purchaseService)
    {
        _uow = uow;
        _mapper = mapper;
        _ollama = ollama;
        _notifier = notifier;
        _purchaseService = purchaseService;
    }

    public async Task<int> CreateAsync(string imageUrl, int? supplierId, CancellationToken ct = default)
    {
        var scan = new AlbaranScan { ImageUrl = imageUrl, SupplierId = supplierId, Status = AlbaranScanStatus.Processing };
        await _uow.Repository<AlbaranScan>().AddAsync(scan, ct);
        await _uow.SaveChangesAsync(ct);
        return scan.Id;
    }

    public async Task ProcessAsync(int scanId, string absoluteImagePath, CancellationToken ct = default)
    {
        var scan = await _uow.Repository<AlbaranScan>().GetByIdAsync(scanId, ct)
            ?? throw new KeyNotFoundException($"AlbaranScan {scanId} not found.");

        string rawResponse;
        try
        {
            var bytes = await File.ReadAllBytesAsync(absoluteImagePath, ct);
            rawResponse = await _ollama.ExtractAlbaranJsonAsync(bytes, ct);
        }
        catch (OllamaUnavailableException ex)
        {
            await MarkFailedAsync(scan, ex.Message, null, ct);
            return;
        }
        catch (IOException ex)
        {
            await MarkFailedAsync(scan, $"No se pudo leer la imagen: {ex.Message}", null, ct);
            return;
        }

        OllamaAlbaranExtraction? extraction;
        try
        {
            extraction = JsonSerializer.Deserialize<OllamaAlbaranExtraction>(ExtractJsonObject(rawResponse), ExtractionJsonOptions);
        }
        catch (JsonException)
        {
            await MarkFailedAsync(scan, "No se pudo interpretar la respuesta del modelo.", rawResponse, ct);
            return;
        }

        if (extraction is null)
        {
            await MarkFailedAsync(scan, "El modelo no devolvió datos.", rawResponse, ct);
            return;
        }

        scan.SupplierNameRaw = extraction.Proveedor;
        scan.AlbaranNumber = extraction.NumeroAlbaran;
        scan.AlbaranDate = DateTime.TryParse(extraction.Fecha, out var date) ? date : null;
        scan.TotalAmount = extraction.Total;
        scan.RawModelResponse = rawResponse;
        scan.Status = AlbaranScanStatus.NeedsReview;

        if (!string.IsNullOrWhiteSpace(extraction.Proveedor))
        {
            var name = extraction.Proveedor.Trim();
            var suppliers = await _uow.Repository<Supplier>().QueryNoTracking().ToListAsync(ct);
            var matches = suppliers.Where(s =>
                s.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(s.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1) scan.SupplierId = matches[0].Id;
        }

        foreach (var line in extraction.Lineas ?? [])
        {
            await _uow.Repository<AlbaranScanLine>().AddAsync(new AlbaranScanLine
            {
                AlbaranScanId = scan.Id,
                Description = line.Descripcion ?? "",
                Quantity = line.Cantidad ?? 0,
                UnitPrice = line.PrecioUnitario ?? 0
            }, ct);
        }

        _uow.Repository<AlbaranScan>().Update(scan);
        await _uow.SaveChangesAsync(ct);
        await _notifier.AlbaranScanUpdatedAsync(scan.Id, scan.Status.ToString());
    }

    public async Task<List<AlbaranScanSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<AlbaranScan>().QueryNoTracking()
            .Include(x => x.Supplier)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return _mapper.Map<List<AlbaranScanSummaryDto>>(list);
    }

    public async Task<AlbaranScanDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var scan = await _uow.Repository<AlbaranScan>().QueryNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return scan is null ? null : _mapper.Map<AlbaranScanDetailDto>(scan);
    }

    public async Task SaveDraftAsync(int id, AlbaranScanEditDto dto, CancellationToken ct = default)
    {
        var scan = await LoadTrackedAsync(id, ct);
        ApplyEdit(scan, dto);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<int> ValidateAndCreatePurchaseAsync(int id, AlbaranScanEditDto dto, CancellationToken ct = default)
    {
        if (dto.SupplierId <= 0)
            throw new InvalidOperationException("Selecciona un proveedor antes de validar.");
        if (dto.Lines.Count == 0 || dto.Lines.Any(l => l.ProductId <= 0))
            throw new InvalidOperationException("Todas las líneas deben tener un producto asignado.");

        var scan = await LoadTrackedAsync(id, ct);
        ApplyEdit(scan, dto);
        await _uow.SaveChangesAsync(ct);

        var purchaseId = await _purchaseService.CreateAsync(new PurchaseFormDto
        {
            SupplierId = dto.SupplierId,
            Date = scan.AlbaranDate ?? DateTime.Now,
            Reference = scan.AlbaranNumber,
            Notes = "Generado desde albarán escaneado",
            Lines = dto.Lines.Select(l => new PurchaseLineFormDto
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitCost = l.UnitPrice
            }).ToList()
        }, ct);

        scan.PurchaseId = purchaseId;
        scan.Status = AlbaranScanStatus.Validated;
        _uow.Repository<AlbaranScan>().Update(scan);
        await _uow.SaveChangesAsync(ct);

        await _notifier.AlbaranScanUpdatedAsync(scan.Id, scan.Status.ToString());
        return purchaseId;
    }

    public async Task RetryAsync(int id, CancellationToken ct = default)
    {
        var scan = await _uow.Repository<AlbaranScan>().GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"AlbaranScan {id} not found.");
        scan.Status = AlbaranScanStatus.Processing;
        scan.ErrorMessage = null;
        _uow.Repository<AlbaranScan>().Update(scan);
        await _uow.SaveChangesAsync(ct);
        await _notifier.AlbaranScanUpdatedAsync(scan.Id, scan.Status.ToString());
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var scan = await _uow.Repository<AlbaranScan>().GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"AlbaranScan {id} not found.");
        _uow.Repository<AlbaranScan>().Remove(scan);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AlbaranScan> LoadTrackedAsync(int id, CancellationToken ct) =>
        await _uow.Repository<AlbaranScan>().Query().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"AlbaranScan {id} not found.");

    private void ApplyEdit(AlbaranScan scan, AlbaranScanEditDto dto)
    {
        scan.SupplierId = dto.SupplierId > 0 ? dto.SupplierId : null;
        scan.AlbaranNumber = dto.AlbaranNumber;
        scan.AlbaranDate = dto.AlbaranDate;
        scan.TotalAmount = dto.Lines.Sum(l => l.Quantity * l.UnitPrice);

        var dtoIds = dto.Lines.Where(l => l.Id > 0).Select(l => l.Id).ToHashSet();
        foreach (var existing in scan.Lines.Where(l => !dtoIds.Contains(l.Id)).ToList())
        {
            scan.Lines.Remove(existing);
            _uow.Repository<AlbaranScanLine>().Remove(existing);
        }

        foreach (var lineDto in dto.Lines)
        {
            var productId = lineDto.ProductId > 0 ? lineDto.ProductId : (int?)null;
            var existing = lineDto.Id > 0 ? scan.Lines.FirstOrDefault(l => l.Id == lineDto.Id) : null;
            if (existing is not null)
            {
                existing.Description = lineDto.Description;
                existing.Quantity = lineDto.Quantity;
                existing.UnitPrice = lineDto.UnitPrice;
                existing.ProductId = productId;
            }
            else
            {
                scan.Lines.Add(new AlbaranScanLine
                {
                    AlbaranScanId = scan.Id,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    ProductId = productId
                });
            }
        }

        _uow.Repository<AlbaranScan>().Update(scan);
    }

    private async Task MarkFailedAsync(AlbaranScan scan, string message, string? rawResponse, CancellationToken ct)
    {
        scan.Status = AlbaranScanStatus.Failed;
        scan.ErrorMessage = message;
        scan.RawModelResponse = rawResponse;
        _uow.Repository<AlbaranScan>().Update(scan);
        await _uow.SaveChangesAsync(ct);
        await _notifier.AlbaranScanUpdatedAsync(scan.Id, scan.Status.ToString());
    }

    /// <summary>Vision models often wrap JSON in prose/markdown despite instructions; this pulls
    /// out the outermost {...} object so parsing still works.</summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start) throw new JsonException("No JSON object found in model response.");
        return text[start..(end + 1)];
    }

    private record OllamaAlbaranExtraction(
        string? Proveedor,
        [property: JsonPropertyName("numero_albaran")] string? NumeroAlbaran,
        string? Fecha,
        List<OllamaAlbaranLineExtraction>? Lineas,
        decimal? Total);

    private record OllamaAlbaranLineExtraction(
        string? Descripcion,
        decimal? Cantidad,
        [property: JsonPropertyName("precio_unitario")] decimal? PrecioUnitario);
}
