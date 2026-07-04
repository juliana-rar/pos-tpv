using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record InvoiceDto(
    int Id, string Number, string OrderNumber, string TableName,
    decimal Subtotal, decimal VatTotal, decimal Total, PaymentMethod PaymentMethod, DateTime CreatedAt);

public record DailyRevenueDto(DateTime Date, decimal Total);

public record PaymentBreakdownDto(PaymentMethod Method, decimal Amount);

public record BillingReportDto(
    decimal Total, decimal VatTotal, int Count, decimal Average,
    IReadOnlyList<InvoiceDto> Invoices,
    IReadOnlyList<DailyRevenueDto> Daily,
    IReadOnlyList<PaymentBreakdownDto> ByMethod);
