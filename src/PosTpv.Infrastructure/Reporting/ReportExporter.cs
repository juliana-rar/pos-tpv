using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PosTpv.Infrastructure.Reporting;

/// <summary>Renders the billing report as CSV, Excel (ClosedXML) or PDF (QuestPDF).</summary>
public class ReportExporter : IReportExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static ReportExporter()
    {
        // Free Community license (valid for small businesses / open use).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportFile ExportBilling(BillingReportDto report, DateTime from, DateTime to, ExportFormat format)
    {
        var stamp = $"{from:yyyyMMdd}-{to:yyyyMMdd}";
        return format switch
        {
            ExportFormat.Csv => new ExportFile(BuildCsv(report), "text/csv", $"billing_{stamp}.csv"),
            ExportFormat.Excel => new ExportFile(BuildExcel(report, from, to),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"billing_{stamp}.xlsx"),
            ExportFormat.Pdf => new ExportFile(BuildPdf(report, from, to), "application/pdf", $"billing_{stamp}.pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static string Money(decimal d) => d.ToString("N2", Inv) + " €";

    // ---- CSV ------------------------------------------------------------------
    private static byte[] BuildCsv(BillingReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Invoice,Order,Table,Method,Date,Subtotal,VAT,Total");
        foreach (var i in report.Invoices)
        {
            sb.Append(Csv(i.Number)).Append(',')
              .Append(Csv(i.OrderNumber)).Append(',')
              .Append(Csv(i.TableName)).Append(',')
              .Append(Csv(i.PaymentMethod.ToString())).Append(',')
              .Append(Csv(i.CreatedAt.ToString("yyyy-MM-dd HH:mm", Inv))).Append(',')
              .Append(i.Subtotal.ToString("F2", Inv)).Append(',')
              .Append(i.VatTotal.ToString("F2", Inv)).Append(',')
              .Append(i.Total.ToString("F2", Inv)).AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine($"Totals,,,,,{report.Total - report.VatTotal:F2},{report.VatTotal.ToString("F2", Inv)},{report.Total.ToString("F2", Inv)}");
        sb.AppendLine($"Invoices,{report.Count}");
        sb.AppendLine($"Average ticket,{report.Average.ToString("F2", Inv)}");

        // UTF-8 BOM so Excel opens accented text / the € sign correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ---- Excel ----------------------------------------------------------------
    private static byte[] BuildExcel(BillingReportDto report, DateTime from, DateTime to)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Billing");

        ws.Cell("A1").Value = "PosTPV — Billing report";
        ws.Range("A1:H1").Merge();
        ws.Cell("A1").Style.Font.SetBold().Font.FontSize = 16;

        ws.Cell("A2").Value = $"Period: {from:yyyy-MM-dd} → {to:yyyy-MM-dd}";
        ws.Range("A2:H2").Merge();
        ws.Cell("A2").Style.Font.FontColor = XLColor.Gray;

        // Summary block
        ws.Cell("A4").Value = "Revenue"; ws.Cell("B4").Value = report.Total;
        ws.Cell("A5").Value = "VAT collected"; ws.Cell("B5").Value = report.VatTotal;
        ws.Cell("A6").Value = "Invoices"; ws.Cell("B6").Value = report.Count;
        ws.Cell("A7").Value = "Average ticket"; ws.Cell("B7").Value = report.Average;
        ws.Range("A4:A7").Style.Font.SetBold();
        ws.Range("B4:B5").Style.NumberFormat.Format = "#,##0.00 €";
        ws.Cell("B7").Style.NumberFormat.Format = "#,##0.00 €";

        // Invoice table
        const int header = 9;
        string[] columns = { "Invoice", "Order", "Table", "Method", "Date", "Subtotal", "VAT", "Total" };
        for (var c = 0; c < columns.Length; c++)
            ws.Cell(header, c + 1).Value = columns[c];

        var head = ws.Range(header, 1, header, columns.Length);
        head.Style.Font.SetBold().Font.FontColor = XLColor.White;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");

        var row = header + 1;
        foreach (var i in report.Invoices)
        {
            ws.Cell(row, 1).Value = i.Number;
            ws.Cell(row, 2).Value = i.OrderNumber;
            ws.Cell(row, 3).Value = i.TableName;
            ws.Cell(row, 4).Value = i.PaymentMethod.ToString();
            ws.Cell(row, 5).Value = i.CreatedAt;
            ws.Cell(row, 5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            ws.Cell(row, 6).Value = i.Subtotal;
            ws.Cell(row, 7).Value = i.VatTotal;
            ws.Cell(row, 8).Value = i.Total;
            row++;
        }

        var moneyCols = ws.Range(header + 1, 6, Math.Max(header + 1, row - 1), 8);
        moneyCols.Style.NumberFormat.Format = "#,##0.00 €";

        if (report.Invoices.Count > 0)
        {
            var table = ws.Range(header, 1, row - 1, columns.Length);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---- PDF ------------------------------------------------------------------
    private static byte[] BuildPdf(BillingReportDto report, DateTime from, DateTime to)
    {
        var indigo = "#4f46e5";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#0f172a"));

                page.Header().Column(col =>
                {
                    col.Item().Text("PosTPV").FontSize(20).Bold().FontColor(indigo);
                    col.Item().Text("Billing report").FontSize(14).SemiBold();
                    col.Item().Text($"Period: {from:yyyy-MM-dd} → {to:yyyy-MM-dd}").FontColor("#64748b");
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(12);

                    // Summary strip
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        Summary(row, "Revenue", Money(report.Total), indigo);
                        Summary(row, "VAT collected", Money(report.VatTotal), "#0ea5e9");
                        Summary(row, "Invoices", report.Count.ToString(), "#22c55e");
                        Summary(row, "Average ticket", Money(report.Average), "#f59e0b");
                    });

                    // Invoice table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);   // invoice
                            c.RelativeColumn(2);   // order
                            c.RelativeColumn(2);   // table
                            c.RelativeColumn(2);   // method
                            c.RelativeColumn(3);   // date
                            c.RelativeColumn(2);   // total
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h, "Invoice", indigo);
                            HeaderCell(h, "Order", indigo);
                            HeaderCell(h, "Table", indigo);
                            HeaderCell(h, "Method", indigo);
                            HeaderCell(h, "Date", indigo);
                            HeaderCell(h, "Total", indigo);
                        });

                        var zebra = false;
                        foreach (var i in report.Invoices)
                        {
                            var bg = zebra ? "#f1f5f9" : "#ffffff";
                            zebra = !zebra;
                            BodyCell(table, i.Number, bg);
                            BodyCell(table, i.OrderNumber, bg);
                            BodyCell(table, i.TableName, bg);
                            BodyCell(table, i.PaymentMethod.ToString(), bg);
                            BodyCell(table, i.CreatedAt.ToString("yyyy-MM-dd HH:mm", Inv), bg);
                            BodyCell(table, Money(i.Total), bg, alignRight: true);
                        }
                    });

                    if (report.Invoices.Count == 0)
                        col.Item().PaddingTop(10).Text("No invoices in this period.").FontColor("#64748b");
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("PosTPV · generated ").FontColor("#94a3b8");
                    t.Span($"{from:yyyy-MM-dd}").FontColor("#94a3b8");
                    t.Span("  ·  Page ").FontColor("#94a3b8");
                    t.CurrentPageNumber().FontColor("#94a3b8");
                    t.Span(" / ").FontColor("#94a3b8");
                    t.TotalPages().FontColor("#94a3b8");
                });
            });
        }).GeneratePdf();
    }

    private static void Summary(RowDescriptor row, string label, string value, string color) =>
        row.RelativeItem().Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor("#64748b");
            c.Item().Text(value).FontSize(13).Bold().FontColor(color);
        });

    private static void HeaderCell(TableCellDescriptor h, string text, string color) =>
        h.Cell().Background(color).Padding(5).Text(text).FontColor("#ffffff").SemiBold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string text, string bg, bool alignRight = false)
    {
        IContainer cell = table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5);
        if (alignRight) cell = cell.AlignRight();
        cell.Text(text).FontSize(9);
    }
}
