using PosTpv.Application.DTOs;

namespace PosTpv.Application.Common.Interfaces;

public enum ExportFormat { Csv, Excel, Pdf }

/// <summary>A rendered export ready to be streamed to the client.</summary>
public record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Renders application reports into downloadable CSV / Excel / PDF documents.</summary>
public interface IReportExporter
{
    ExportFile ExportBilling(BillingReportDto report, DateTime from, DateTime to, ExportFormat format);
}
