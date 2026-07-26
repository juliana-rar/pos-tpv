using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IBillingService
{
    Task<BillingReportDto> GetReportAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<InvoiceDetailDto?> GetInvoiceDetailAsync(int invoiceId, CancellationToken ct = default);
}

public class BillingService : IBillingService
{
    private readonly IUnitOfWork _uow;

    public BillingService(IUnitOfWork uow) => _uow = uow;

    public async Task<BillingReportDto> GetReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var toExclusive = to.Date.AddDays(1);

        var invoices = await _uow.Repository<Invoice>().Query()
            .Include(i => i.Order).ThenInclude(o => o.Table)
            .Where(i => i.CreatedAt >= from.Date && i.CreatedAt < toExclusive)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceDto(
                i.Id, i.Number, i.Order.Number, i.Order.Table.Name,
                i.Subtotal, i.VatTotal, i.Total, i.PaymentMethod, i.CreatedAt))
            .ToListAsync(ct);

        var total = invoices.Sum(i => i.Total);
        var vat = invoices.Sum(i => i.VatTotal);
        var avg = invoices.Count == 0 ? 0m : total / invoices.Count;

        // Daily revenue across every day in the range (zero-filled so the chart has no gaps).
        var byDay = invoices.GroupBy(i => i.CreatedAt.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        var daily = new List<DailyRevenueDto>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            daily.Add(new DailyRevenueDto(d, byDay.TryGetValue(d, out var v) ? v : 0m));

        // Breakdown by actual tender (accurate for split payments, unlike invoice.PaymentMethod).
        var byMethod = await _uow.Repository<Payment>().Query()
            .Where(p => p.Invoice.CreatedAt >= from.Date && p.Invoice.CreatedAt < toExclusive)
            .GroupBy(p => p.Method)
            .Select(g => new PaymentBreakdownDto(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(ct);

        return new BillingReportDto(total, vat, invoices.Count, avg, invoices, daily, byMethod);
    }

    public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await _uow.Repository<Invoice>().QueryNoTracking()
            .Include(i => i.Order).ThenInclude(o => o.Table)
            .Include(i => i.Order).ThenInclude(o => o.Waiter)
            .Include(i => i.Order).ThenInclude(o => o.Items).ThenInclude(li => li.Product)
            .Include(i => i.Order).ThenInclude(o => o.Items).ThenInclude(li => li.Extras)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return null;

        var lines = invoice.Order.Items.Select(li => new InvoiceLineDto(
            li.Product.Name, li.Quantity, li.UnitPrice, li.LineGross,
            li.Extras.Select(e => e.Name).ToList(), li.Comment)).ToList();

        var payments = invoice.Payments.Select(p => new InvoicePaymentDto(p.Method, p.Amount)).ToList();

        return new InvoiceDetailDto(
            invoice.Id, invoice.Number, invoice.Order.Number, invoice.Order.Table.Name, invoice.Order.Waiter.FullName,
            invoice.Subtotal, invoice.VatTotal, invoice.Total, invoice.PaymentMethod, invoice.CreatedAt,
            lines, payments);
    }
}
