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

public record InvoiceLineDto(
    string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal,
    IReadOnlyList<string> Extras, string? Comment);

public record InvoicePaymentDto(PaymentMethod Method, decimal Amount);

public record InvoiceDetailDto(
    int Id, string Number, string OrderNumber, string TableName, string WaiterName,
    decimal Subtotal, decimal VatTotal, decimal Total, PaymentMethod PaymentMethod, DateTime CreatedAt,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<InvoicePaymentDto> Payments);
