using Microsoft.EntityFrameworkCore;
using VirusDetectionApp.Models;

namespace VirusDetectionApp.Data;

// Database connecting EF Core to the SQLite database
public class AppDbContext : DbContext
{
    //Its 'base(option)' so you can swap out database engines and that it knows its handling a db.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

//Declares the submission table in the db.
    public DbSet<Submission> Submissions => Set<Submission>();
}