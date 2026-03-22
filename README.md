# Virus Detection App

A Blazor web application that analyzes files for viruses using the VirusTotal API. Users can upload files, track submissions, and export submission history.

## Setup Instructions

### Prerequisites
- **.NET 8 SDK** or later installed on your machine ([Download](https://dotnet.microsoft.com/download))
- **A VirusTotal API key** (free account available at https://www.virustotal.com)

### Installation Steps

1.  **OPTION A: Clone the repository** (if applicable):
   ```bash
   git clone https://github.com/anouk-aa/VirusDetectionApp.git
   cd VirusDetectionApp
   ```
   - then in the same trajectory enter 'code .'
   - If that doesn't wprk open it manually in Visual Studio Code.


   **OPTION B: Maunal Extraction**
    - Download and extract zipped file from https://github.com/anouk-aa/VirusDetectionApp.git>
	-	Open Visual Studio
	-	Click “Open a project or solution”
	-	Navigate to your folder where you extracted the code

2. **Configure your VirusTotal API Key**:
   - Open `appsettings.json` in the project root
   - Replace the `ApiKey` value with your own VirusTotal API key:
     ```json
     {
       "VirusTotal": {
         "ApiKey": "YOUR_API_KEY_HERE"
       }
     }
     ```

3. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

4. **Run the application**:
   ```bash
   dotnet run
   ```
   Then run the app on http://localhost:5143 in a browser.

## Configuration

### VirusTotal API Key

The application requires a VirusTotal API key for file analysis. 

**Location**: `appsettings.json`

```json
{
  "VirusTotal": {
    "ApiKey": "YOUR_API_KEY_HERE"
  }
}
```

**Getting an API Key**:
1. Visit https://www.virustotal.com
2. Create a free account or sign in
3. Navigate to your API settings
4. Copy your API key and paste it in `appsettings.json`

**Note**: Keep your API key secure and never commit it to version control. Consider using `appsettings.Development.json` for local development with sensitive values.

## Notes and Assumptions

### Architecture
- **Framework**: ASP.NET Core 8 with Blazor components for server-side rendering
- **Database**: SQLite for local data persistence (file: `virussubmissions.db`)
- **External API**: VirusTotal API for file analysis

### Database
- The application automatically creates the SQLite database on first run
- Submissions are stored with their analysis ID, file hash, and current status
- Includes timestamps for tracking when files were submitted

### File Uploads
- Uploaded files are temporarily stored in the `Uploads/` directory
- File analysis is handled asynchronously via a background service
- Multiple antivirus engines are used by VirusTotal for comprehensive scanning

### Background Processing
- `SubmissionBackgroundService` periodically polls VirusTotal for analysis results
- Submissions are tracked and automatically updated when results become available
- Results can be exported to Excel format for reporting

### Export Functionality
- Submission history can be exported to Excel files using `ExportService`
- Includes submission details, file hashes, and analysis results

## Troubleshooting

- **Database errors**: Delete `virussubmissions.db` to reset the database
- **API connection issues**: Verify your API key and internet connectivity
- **Port already in use**: The default HTTPS port (5001) may be in use; check with `lsof -i :5001`

## Process Overview - (After API registration for VirsusTotalService)

### 1. Initial Creation of Database
- Model was first created to structure the database in 'Submission.cs'.
- Next I created the 'AppDbContext.cs' to connect to SQLite, to create a databses with a table in on runtime in 'Program.cs'.


Creates the Context for the Databases and Table
```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Submission> Submissions => Set<Submission>();
}
```

- Then I registred DbContext in the 'Program.cs', where it also creates the database's name.


//Registration of the Database
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=virussubmissions.db"));
```

- I then added the database creation logic (Program.cs)


//Creates temporary container to connect to the AppDbContext.cs through the the app's dependency injection.
//Creates the databse and table if they don't exist.
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); 
    var created = db.Database.EnsureCreated();
}
```

## 2. Processing and Uploading of Sumbission 

### 2.1.  VirusTotalService Set up 'apsettings.json'

- I set my API key which I got from https://www.virustotal.com  which will be needed in the virus detection passing service.

{
  "VirusTotal": {
    "ApiKey": "insert_your_api_key_here"
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },

  "AllowedHosts": "*"
}

### 2.2.  VirusTotalService Creation 'VirusTotalService.cs'

#### 2.2.1. VirusTotalService's Constructor
- I first registered the service in 'Program.cs'

    builder.Services.AddHttpClient<VirusTotalService>();

- Then I proceeded tocreated VirusTotalService.cs and it's class that sends the submission through to VirusTotal.
- I  declared private readonly fields that get their values automatically from ASP.NET Core's Dependency Injection (DI) container and then set it up for ussage.
- I set the API key's and checked and checked if the API key is missing or empty.
- I then configured the HttpClient with the virus api Key.
- To do this I set a base url: https://www.virustotal.com/api/v3/ for all the all HttpClient requests to VirusTotal API, Added the API key to the default request headers and added a "application/json" to the Accept header (tells API we want JSON responses).



```csharp
public VirusTotalService(HttpClient httpClient, IConfiguration configuration)
{
    _httpClient = httpClient;
    _configuration = configuration;

    var apiKey = _configuration["VirusTotal:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new Exception("VirusTotal API key is missing in appsettings.json");
    }

    _httpClient.BaseAddress = new Uri("https://www.virustotal.com/api/v3/");

    _httpClient.DefaultRequestHeaders.Clear();
    _httpClient.DefaultRequestHeaders.Add("x-apikey", apiKey);
    _httpClient.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
}
```

#### 2.2.2. VirusTotalService's UploadFileAsync Method
- I created an async method that takes file stream and name, returns analysis ID string.
- It used aync because it lets you do other things while waiting for answers from VirusTotal.
- I created a multipart form content object for file upload (automatically deleted from memory after done).
- I created a stream content from the file stream (automatically deleted from memory after done).
- I created its scope and registered it in 'Program.cs' which also allows it to test the connection first.



```csharp
using (var scope = app.Services.CreateScope())
{
    var virusTotalService = scope.ServiceProvider.GetRequiredService<VirusTotalService>();
    var result = await virusTotalService.TestConnectionAsync();
    app.Logger.LogInformation("VirusTotal response: {Result}", result);
}
```
- I set the content type to binary as its the format required for file uplaods.
- I allowed it to add the file content to the multipart form with name "file" and the filename. (multipart)
- I created a post request to "files" endpoint with the multipart content.
- It reads the reponse as a JSON string.
- It throws an excepton with erro details if upload failed.
- If succesful, it parses the JSON response into a document for reading (automatically deleted from memory after done).
- It then extracts the analysisId from the JSON, navigates to "data" and  from that the "id" property in JSON and then gets the string value of the analysisId.



```csharp
public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
{
    using var content = new MultipartFormDataContent();

    using var fileContent = new StreamContent(fileStream);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

    content.Add(fileContent, "file", fileName);

    var response = await _httpClient.PostAsync("files", content);
    var json = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"VirusTotal upload failed: {json}");
    }

    using var document = JsonDocument.Parse(json);

    var analysisId = document.RootElement
        .GetProperty("data")
        .GetProperty("id")
        .GetString();

    if (string.IsNullOrWhiteSpace(analysisId))
    {
        throw new Exception("VirusTotal did not return an analysis ID.");
    }

    return analysisId;
}
```

### 2.3.  Sumbission Creation 'Submission.cs'

#### 2.3.1. Submission's Database Connection Setup
- I first registered the submission service in 'Program.cs'
    builder.Services.AddScoped<SubmissionService>();

- I continued to to create the 'Submission.cs' service that allows you to save the submissions to the database.
- It then defines the service and then set up a vraiable for AppDbContext.
- It then injects the AppDbContext into the variable for ussage.

### 2.3.2. Sumbission Method
- Gets incoming submission and adds it to the database.
- Awaits to save changes so that it makes sure your new or updated data is actually written to the database.


```csharp
public async Task AddSubmissionAsync(Submission submission)
{
    _db.Submissions.Add(submission);
    await _db.SaveChangesAsync();
}
```

#### 2.3.3. Get All Sumbission Method
- Gets all submissions, newest first, without tracking changes.
- Uses awaits for the database to finish fetching the data, then returns the result.


```csharp
public async Task<List<Submission>> GetAllSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}
```

#### 2.3.3. Get Pending Sumbissions Method
- Gets only submissions that are still being processed.
- Uses awaits for the database to finish fetching the data, then returns the result.
- This will be used in the 'SubmissionBackgroundService.cs', because the app needs to know which files are still waiting for virus scan results. This lets the background service keep checking only those files, instead of re-checking everything or missing update.


```csharp
public async Task<List<Submission>> GetPendingSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .Where(s => s.Status != "Completed" && s.Status != "Failed")
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}
```


#### 2.3.4. Get Sumbission by ID Method
- Finds a specific submission by its ID.
- Uses awaits for the database to finish fetching the data, then returns the result.
- When you want to show details or update the status of a specific file (like when a scan finishes), you need a way to find that exact submission. This method makes sure you always get the right record to display or update.


```csharp
public async Task<Submission?> GetSubmissionByIdAsync(int id)
{
    return await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
}
```

#### 2.3.4. Update Sumbission Method
- Updates an existing submission in the database.
- waits to save changes so that it makes sure your new or updated data is actually written to the database.



```csharp
public async Task UpdateSubmissionAsync(Submission submission)
{
    _db.Submissions.Update(submission);
    await _db.SaveChangesAsync();
}
```

#### 2.3.5. Update Sumbission Status Method
- Finds a submission by ID and updates its status and summary.
- This method is activated through the 'SubmissionBackgroundService.cs'.


```csharp
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
```

#### 2.3.6. Export Sumbission Method
- Gets all submissions for exporting (e.g., to Excel).
- Awaits for the database to finish up before exporting


```csharp
public async Task<List<Submission>> ExportSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}
```


### 2.4.  Export Service Creation 'ExportService.cs'
- Registers service firts in 'Program.cs'.

    builder.Services.AddScoped<ExportService>();

- Defines new HTTP GET endpoint at /export/submissions in 'Program.cs'.

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

- In 'ExportService.cs':
- Creates a new Excel workbook and adds a worksheet named "Submissions".
- Fills the first row with column names for each property.
- Loops through all submissions and add their data.
- Makes all columns wide enough to fit their content.
- Save the workbook to a memory stream and return as bytes.
- Saves the file to memory not a disk.
- Returns the file as a byte array, ready for download.


```csharp
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
```

### 2.5.  Backgroound Service for Submission Creation 'SubmissionBackgroundService.cs'
- I first registered the service in 'Program.cs'

    builder.Services.AddHostedService<SubmissionBackgroundService>();

- Defines a background service that runs continuously in the background of your app.
- Declares private fields for the service provider (to resolve dependencies) and logger (for logging).
- Constructor uses dependency injection to assign the service provider and logger to those fields.
- When the service starts, it logs that it has started.
- Main loop: keeps running until the app is shutting down. Each cycle:
- reates a new DI scope to safely resolve services for this cycle.
- Gets the database context and VirusTotal service from the DI container.
- Finds all submissions that are "Queued" (waiting to be uploaded to VirusTotal).
- For each queued submission:
  - Logs that it’s processing the submission.
  - Checks if the file exists on disk. If not, marks as "Failed" and logs why.
  - If the file exists, uploads it to VirusTotal (with retry logic for reliability).
  - Updates the submission’s status to "In Progress" and saves the new analysis ID.
  - Logs all status changes and actions.
  - If upload fails, marks as "Failed", logs the error, and deletes the file.
- Finds all submissions that are "In Progress" (already uploaded, waiting for scan results).
- For each in-progress submission:
  - Logs that it’s polling VirusTotal for results.
  - Polls VirusTotal for the latest scan result (with retry logic).
  -  If the scan is "Completed", updates status and scan summary with results.
  - If still "In Progress" or "Queued", keeps status as "In Progress".
  - If an unexpected status or error, marks as "Failed" and logs details.
  - After completion or failure, deletes the uploaded file from disk.
  - Logs all status changes and actions.
  - If polling fails, marks as "Failed" and logs the error.
- Catches and logs any errors in the main loop so the service doesn’t crash.
- Waits 10 seconds before starting the next cycle (to avoid overloading the API or database).



```csharp
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

                        await using var stream = File.OpenRead(submission.FilePath);

                        var analysisId = await RetryAsync(
                             () => virusTotalService.UploadFileAsync(stream, submission.FileName),
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
```

### 2.4. More Information: How The Files Get Uploaded
- The background service (SubmissionBackgroundService) finds a queued submission and opens the file from disk.
- It calls virusTotalService.UploadFileAsync(stream, submission.FileName).
- Inside UploadFileAsync, the file stream is sent to VirusTotal using an HTTP POST request to the "files" endpoint.


### 3. Razor Pages
 
#### 3.1 Upload Razor

- Provides a file upload form for users to submit files for virus scanning.
- Uses an `<InputFile>` component to allow file selection from the user's device.
- Includes additional form fields for source and reason for suspicion.
- Handles file selection and form submission events in C# code-behind.
- On submit, saves the uploaded file to the server and creates a new submission record in the database.
- Displays upload progress and error messages to the user.
- Triggers background processing for virus scanning after upload.

#### 3.2 Submission Razor

- Displays a list of all file submissions made by users.
- Retrieves submission data from the database using a service (e.g., `SubmissionService`).
- Shows key details for each submission: file name, source, reason for suspicion, submission date, status, and scan summary.
- Updates the UI automatically as new submissions are added or statuses change.
- Provides visual indicators for submission status (e.g., queued, in progress, completed, failed).
- May include options to filter, sort, or search submissions.
- Allows users to view detailed scan results for each submission.
- Supports exporting submission history to Excel via the export feature.



## Favourite Punk, Emo, or Hard Rock band

- **Guns N' Roses**
