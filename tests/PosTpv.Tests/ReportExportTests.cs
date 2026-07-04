using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Enums;
using PosTpv.Infrastructure.Reporting;
using Xunit;

namespace PosTpv.Tests;

/// <summary>Pure (no-DB) checks that each export format renders a well-formed document.</summary>
public class ReportExportTests
{
    private static BillingReportDto Sample()
    {
        var invoices = new List<InvoiceDto>
        {
            new(1, "INV-000001", "O-00001", "T1", 9.09m, 0.91m, 10.00m, PaymentMethod.Cash, new DateTime(2026, 7, 1, 20, 30, 0)),
            new(2, "INV-000002", "O-00002", "T3", 18.18m, 1.82m, 20.00m, PaymentMethod.Card, new DateTime(2026, 7, 2, 21, 05, 0)),
        };
        var daily = new List<DailyRevenueDto>
        {
            new(new DateTime(2026, 7, 1), 10.00m),
            new(new DateTime(2026, 7, 2), 20.00m),
        };
        var byMethod = new List<PaymentBreakdownDto>
        {
            new(PaymentMethod.Cash, 10.00m),
            new(PaymentMethod.Card, 20.00m),
        };
        return new BillingReportDto(30.00m, 2.73m, 2, 15.00m, invoices, daily, byMethod);
    }

    [Fact]
    public void Csv_export_is_utf8_with_header()
    {
        var file = new ReportExporter().ExportBilling(Sample(), new(2026, 7, 1), new(2026, 7, 4), ExportFormat.Csv);
        Assert.EndsWith(".csv", file.FileName);
        // UTF-8 BOM then the header line.
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.Content.Take(3));
        Assert.Contains("Invoice,Order,Table", System.Text.Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public void Excel_export_is_a_valid_xlsx_zip()
    {
        var file = new ReportExporter().ExportBilling(Sample(), new(2026, 7, 1), new(2026, 7, 4), ExportFormat.Excel);
        Assert.EndsWith(".xlsx", file.FileName);
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, file.Content.Take(4)); // "PK.." ZIP magic
    }

    [Fact]
    public void Pdf_export_starts_with_pdf_magic()
    {
        var file = new ReportExporter().ExportBilling(Sample(), new(2026, 7, 1), new(2026, 7, 4), ExportFormat.Pdf);
        Assert.EndsWith(".pdf", file.FileName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.Content.Take(4).ToArray()));
        Assert.True(file.Content.Length > 1000);
    }
}
