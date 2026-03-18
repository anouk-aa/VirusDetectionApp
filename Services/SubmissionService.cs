using Microsoft.EntityFrameworkCore;
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

    public async Task UpdateSubmissionAsync(Submission submission)
    {
        _db.Submissions.Update(submission);
        await _db.SaveChangesAsync();
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
}