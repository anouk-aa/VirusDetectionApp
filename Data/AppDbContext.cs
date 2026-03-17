using Microsoft.EntityFrameworkCore;
using VirusDetectionApp.Models;

namespace VirusDetectionApp.Data;

// Database connecting EF Core to the SQLite database
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Submission> Submissions => Set<Submission>();
}