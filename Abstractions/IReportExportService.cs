using TestingSystem.Models;

namespace TestingSystem.Abstractions;

/// <summary>Експорт даних. Реалізація (CSV, XLSX, PDF) прихована за інтерфейсом і є замінною.</summary>
public interface IReportExportService
{
    void ExportResultsCsv(IReadOnlyList<ResultExportRow> rows, string path);
    void ExportResultsExcel(IReadOnlyList<ResultExportRow> rows, string path);
    void ExportReportPdf(ReportData report, string path);
    void ExportCertificatePdf(CertificateData certificate, string path);
}
