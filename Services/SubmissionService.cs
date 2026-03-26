using Microsoft.EntityFrameworkCore;
using System.IO;
using VirusDetectionApp.Data;
using VirusDetectionApp.Models;

namespace VirusDetectionApp.Services;

public class SubmissionService
{
    private readonly AppDbContext _db;

    public SubmissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddSubmissionAsync(Submission submission)
    {
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Submission>> GetAllSubmissionsAsync()
    {
        return await _db.Submissions
            .AsNoTracking()
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    //This is so that it can check which sumbissions are still processing
    //This is used in the logs
    public async Task<List<Submission>> GetPendingSubmissionsAsync()
    {
        return await _db.Submissions
            .AsNoTracking()
            .Where(s => s.Status != "Completed" && s.Status != "Failed")
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<Submission?> GetSubmissionByIdAsync(int id)
    {
        return await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
    }

    //The status and scansummary needs to be updated based of its completion state
    public async Task UpdateSubmissionStatusAsync(
        int id,
        string status,
        string? scanSummary = null)
    {
        var submission = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return;
        }

        submission.Status = status;
        submission.ScanSummary = scanSummary;

        await _db.SaveChangesAsync();
    }

    public async Task<List<Submission>> ExportSubmissionsAsync()
    {
        return await _db.Submissions
            .AsNoTracking()
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteSubmissionAsync(int id)
    {
        var submission = await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(submission.FilePath) && File.Exists(submission.FilePath))
        {
            File.Delete(submission.FilePath);
        }

        _db.Submissions.Remove(submission);
        await _db.SaveChangesAsync();
        return true;
    }
}