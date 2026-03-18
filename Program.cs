using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirusDetectionApp.Components;
using VirusDetectionApp.Data;
using VirusDetectionApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=virussubmissions.db"));

// Services
builder.Services.AddHttpClient<VirusTotalService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddHostedService<SubmissionBackgroundService>();

// Razor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Ensure database exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var created = db.Database.EnsureCreated();
    app.Logger.LogInformation("Database created: {Created}", created);
}

// Optional VirusTotal connection test
using (var scope = app.Services.CreateScope())
{
    var virusTotalService = scope.ServiceProvider.GetRequiredService<VirusTotalService>();
    var result = await virusTotalService.TestConnectionAsync();
    app.Logger.LogInformation("VirusTotal response: {Result}", result);
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Excel export endpoint
app.MapGet("/export/submissions", async (
    SubmissionService submissionService,
    ExportService exportService) =>
{
    var submissions = await submissionService.ExportSubmissionsAsync();
    var fileBytes = exportService.CreateSubmissionsExcel(submissions);

    return Results.File(
        fileBytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "submissions.xlsx");
});

app.Run();