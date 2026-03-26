namespace VirusDetectionApp.Models;

//WHY IS Status SET TO Queued?
//Step: Upload file-> (Status) Queued-> (ScanSummary) null
//Step: Upload file-> (Status) In Progress-> (ScanSummary) null
//Step: Upload file-> (Status) Completed-> (ScanSummary) e.g. 5/70


public class Submission
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string Source { get; set; } = "";
    public string ReasonForSuspicion { get; set; } = "";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Queued";
    public string? ScanSummary { get; set; }
    public string? AnalysisId { get; set; }
    public string FilePath { get; set; } = "";
}