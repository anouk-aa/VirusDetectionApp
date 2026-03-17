using ClosedXML.Excel;
using VirusDetectionApp.Models;

namespace VirusDetectionApp.Services;

public class ExportService
{
    public byte[] CreateSubmissionsExcel(List<Submission> submissions)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Submissions");

        worksheet.Cell(1, 1).Value = "Id";
        worksheet.Cell(1, 2).Value = "File Name";
        worksheet.Cell(1, 3).Value = "Source";
        worksheet.Cell(1, 4).Value = "Reason For Suspicion";
        worksheet.Cell(1, 5).Value = "Submitted At";
        worksheet.Cell(1, 6).Value = "Status";
        worksheet.Cell(1, 7).Value = "Scan Summary";
        worksheet.Cell(1, 8).Value = "Analysis Id";

        for (int i = 0; i < submissions.Count; i++)
        {
            var submission = submissions[i];
            var row = i + 2;

            worksheet.Cell(row, 1).Value = submission.Id;
            worksheet.Cell(row, 2).Value = submission.FileName;
            worksheet.Cell(row, 3).Value = submission.Source;
            worksheet.Cell(row, 4).Value = submission.ReasonForSuspicion;
            worksheet.Cell(row, 5).Value = submission.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 6).Value = submission.Status;
            worksheet.Cell(row, 7).Value = submission.ScanSummary ?? "";
            worksheet.Cell(row, 8).Value = submission.AnalysisId ?? "";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}