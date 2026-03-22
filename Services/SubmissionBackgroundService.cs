using Microsoft.EntityFrameworkCore;
using VirusDetectionApp.Data;
using System.IO;

namespace VirusDetectionApp.Services;

public class SubmissionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubmissionBackgroundService> _logger;

    public SubmissionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SubmissionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Submission background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var virusTotalService = scope.ServiceProvider.GetRequiredService<VirusTotalService>();

                var queuedSubmissions = await db.Submissions
                    .Where(s => s.Status == "Queued" && s.AnalysisId == null)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation("Queued submissions found: {Count}", queuedSubmissions.Count);

                foreach (var submission in queuedSubmissions)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Processing queued submission {Id} for file {FileName}.",
                            submission.Id,
                            submission.FileName);

                        if (!File.Exists(submission.FilePath))
                        {
                            submission.Status = "Failed";
                            submission.ScanSummary = "Uploaded file not found on disk.";

                            _logger.LogInformation(
                                "Submission {Id} status changed to {Status}. Summary: {Summary}",
                                submission.Id,
                                submission.Status,
                                submission.ScanSummary);

                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        var analysisId = await RetryAsync(
                            async () =>
                            {
                                await using var stream = File.OpenRead(submission.FilePath);
                                return await virusTotalService.UploadFileAsync(stream, submission.FileName);
                            },
                            "VirusTotal file upload");

                        submission.AnalysisId = analysisId;
                        submission.Status = "In Progress";
                        submission.ScanSummary = "File uploaded. Waiting for scan completion...";

                        _logger.LogInformation(
                            "Submission {Id} status changed to {Status}. Summary: {Summary}",
                            submission.Id,
                            submission.Status,
                            submission.ScanSummary);

                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Submission {Id} uploaded to VirusTotal.", submission.Id);
                    }
                    catch (Exception ex)
                    {
                        submission.Status = "Failed";
                        submission.ScanSummary = $"Upload failed: {ex.Message}";

                        _logger.LogInformation(
                            "Submission {Id} status changed to {Status}. Summary: {Summary}",
                            submission.Id,
                            submission.Status,
                            submission.ScanSummary);

                        await db.SaveChangesAsync(stoppingToken);

                        // Delete the uploaded file after upload fails
                        if (!string.IsNullOrWhiteSpace(submission.FilePath) && File.Exists(submission.FilePath))
                        {
                            try
                            {
                                File.Delete(submission.FilePath);
                                _logger.LogInformation("Deleted file for failed submission {Id}: {FilePath}", submission.Id, submission.FilePath);
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning(deleteEx, "Failed to delete file for submission {Id}: {FilePath}", submission.Id, submission.FilePath);
                            }
                        }

                        _logger.LogError(ex, "Failed uploading submission {Id}.", submission.Id);
                    }
                }

                var inProgressSubmissions = await db.Submissions
                    .Where(s => s.Status == "In Progress" && s.AnalysisId != null)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation("In-progress submissions found: {Count}", inProgressSubmissions.Count);

                foreach (var submission in inProgressSubmissions)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Polling VirusTotal for submission {Id} with analysis ID {AnalysisId}.",
                            submission.Id,
                            submission.AnalysisId);

                        var result = await RetryAsync(
                             () => virusTotalService.GetAnalysisAsync(submission.AnalysisId!),
                            "VirusTotal analysis polling");

                        if (result.Status == "Completed")
                        {
                            submission.Status = "Completed";
                            submission.ScanSummary = $"{result.Malicious} malicious / {result.Harmless} harmless";
                        }
                        else if (result.Status == "Queued" || result.Status == "In Progress")
                        {
                            submission.Status = "In Progress";
                            submission.ScanSummary = "Scan in progress...";
                        }
                        else
                        {
                            submission.Status = "Failed";
                            submission.ScanSummary = $"Unexpected status: {result.Status}";
                        }

                        _logger.LogInformation(
                            "Submission {Id} status changed to {Status}. Summary: {Summary}",
                            submission.Id,
                            submission.Status,
                            submission.ScanSummary);

                        await db.SaveChangesAsync(stoppingToken);

                        // Delete the uploaded file after analysis completes or fails
                        if ((submission.Status == "Completed" || submission.Status == "Failed") && 
                            !string.IsNullOrWhiteSpace(submission.FilePath) && 
                            File.Exists(submission.FilePath))
                        {
                            try
                            {
                                File.Delete(submission.FilePath);
                                _logger.LogInformation("Deleted file for submission {Id}: {FilePath}", submission.Id, submission.FilePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete file for submission {Id}: {FilePath}", submission.Id, submission.FilePath);
                            }
                        }

                        _logger.LogInformation(
                            "Submission {Id} updated to status {Status}.",
                            submission.Id,
                            submission.Status);
                    }
                    catch (Exception ex)
                    {
                        submission.Status = "Failed";
                        submission.ScanSummary = $"Polling failed: {ex.Message}";

                        _logger.LogInformation(
                            "Submission {Id} status changed to {Status}. Summary: {Summary}",
                            submission.Id,
                            submission.Status,
                            submission.ScanSummary);

                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogError(ex, "Failed checking submission {Id}.", submission.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background service failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action, string operationName, int attempts = 3)
    {
        Exception? lastException = null;

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;

                _logger.LogWarning(
                    ex,
                    "{Operation} failed on attempt {Attempt} of {Attempts}.",
                    operationName,
                    i + 1,
                    attempts);

                await Task.Delay(TimeSpan.FromSeconds(i + 1));
            }
        }

        throw lastException ?? new Exception($"{operationName} failed after retries.");
    }
}