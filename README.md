# Virus Detection App

A Blazor web application that analyzes files for viruses using the VirusTotal API. Users can upload files, track submissions, and export submission history.

## Setup Instructions

### Prerequisites
- **.NET 8 SDK** or later installed on your machine ([Download](https://dotnet.microsoft.com/download))
- **A VirusTotal API key** (free account available at https://www.virustotal.com)

### Installation Steps

1. **Clone the repository** (if applicable):
   ```bash
   git clone <repository-url>
   cd VirusDetectionApp
   ```

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
   The app will start at `https://localhost:5001` by default.

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

### Process Overview - (After API registration for VirsusTotalService)

### 1. Initial Creation of Database
- Model was first created to structure the database in 'Submission.cs'.
- Next I created the 'AppDbContext.cs' to connect to SQLite, to create a databses with a table in on runtime in 'Program.cs'.

Creates the Context for the Databases and Table 
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Submission> Submissions => Set<Submission>();
}

- Then I registred DbContext in the 'Program.cs', where it also creates the database's name.

//Registration of the Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=virussubmissions.db"));

- I then added the database creation logic (Program.cs)

//Creates temporary container to connect to the AppDbContext.cs through the the app's dependency injection.
//Creates the databse and table if they don't exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); 
    var created = db.Database.EnsureCreated();
}

## 2. Processing and Uploading of Sumbission 

## 2.1.  VirusTotalService Set up 'apsettings.json'

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

## 2.2.  VirusTotalService Creation 'VirusTotalService.cs'

## 2.2.1. VirusTotalService's Constructor
- I first registered the service in 'Program.cs'

    builder.Services.AddHttpClient<VirusTotalService>();

- Then I proceeded tocreated VirusTotalService.cs and it's class that sends the submission through to VirusTotal.
- I  declared private readonly fields that get their values automatically from ASP.NET Core's Dependency Injection (DI) container and then set it up for ussage.
- I set the API key's and checked and checked if the API key is missing or empty.
- I then configured the HttpClient with the virus api Key.
- To do this I set a base url: https://www.virustotal.com/api/v3/ for all the all HttpClient requests to VirusTotal API, Added the API key to the default request headers and added a "application/json" to the Accept header (tells API we want JSON responses).


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

## 2.2.2. VirusTotalService's UploadFileAsync Method
- I created an async method that takes file stream and name, returns analysis ID string and reg
- It used aync because it lets you do other things while waiting for answers from VirusTotal.
- I created a multipart form content object for file upload (automatically deleted from memory after done).
- I created a stream content from the file stream (automatically deleted from memory after done).
- I created its scope and registered it in 'Program.cs' which also allows it to test the connection first.


    using (var scope = app.Services.CreateScope())
    {
        var virusTotalService = scope.ServiceProvider.GetRequiredService<VirusTotalService>();
        var result = await virusTotalService.TestConnectionAsync();
        app.Logger.LogInformation("VirusTotal response: {Result}", result);
    }
- I set the content type to binary as its the format required for file uplaods.
- I allowed it to add the file content to the multipart form with name "file" and the filename. (multipart)
- I created a post request to "files" endpoint with the multipart content.
- It reads the reponse as a JSON string.
- It throws an excepton with erro details if upload failed.
- If succesful, it parses the JSON response into a document for reading (automatically deleted from memory after done).
- It then extracts the analysisId from the JSON, navigates to "data" and  from that the "id" property in JSON and then gets the string value of the analysisId.


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

## 2.2.  Sumbission Creation 'Submission.cs'

## 2.2.1. Submission's Database Connection Setup
- I first registered the submission service in 'Program.cs'
    builder.Services.AddScoped<SubmissionService>();

- I continued to to create the 'Submission.cs' service that allows you to save the submissions to the database.
- It then defines the service and then set up a vraiable for AppDbContext.
- It then injects the AppDbContext into the variable for ussage.

## 2.2.2. Sumbission Method
- Gets incoming submission and adds it to the database.
- Awaits to save changes so that it makes sure your new or updated data is actually written to the database.

public async Task AddSubmissionAsync(Submission submission)
{
    _db.Submissions.Add(submission);
    await _db.SaveChangesAsync();
}

## 2.2.3. Get All Sumbission Method
- Gets all submissions, newest first, without tracking changes.
- Uses awaits for the database to finish fetching the data, then returns the result.

public async Task<List<Submission>> GetAllSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}

## 2.2.3. Get Pending Sumbissions Method
- Gets only submissions that are still being processed.
- Uses awaits for the database to finish fetching the data, then returns the result.
- This will be used in the 'SubmissionBackgroundService.cs', because the app needs to know which files are still waiting for virus scan results. This lets the background service keep checking only those files, instead of re-checking everything or missing update.

public async Task<List<Submission>> GetPendingSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .Where(s => s.Status != "Completed" && s.Status != "Failed")
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}


## 2.2.4. Get Sumbission by ID Method
- Finds a specific submission by its ID.
- Uses awaits for the database to finish fetching the data, then returns the result.
- When you want to show details or update the status of a specific file (like when a scan finishes), you need a way to find that exact submission. This method makes sure you always get the right record to display or update.

public async Task<Submission?> GetSubmissionByIdAsync(int id)
{
    return await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id);
}

## 2.2.4. Update Sumbission Method
- Updates an existing submission in the database.
- waits to save changes so that it makes sure your new or updated data is actually written to the database.


public async Task UpdateSubmissionAsync(Submission submission)
{
    _db.Submissions.Update(submission);
    await _db.SaveChangesAsync();
}

## 2.2.5. Update Sumbission Status Method
- Finds a submission by ID and updates its status and summary.
- This method is activated through the 'SubmissionBackgroundService.cs'.

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

## 2.2.6. Export Sumbission Method
- Gets all submissions for exporting (e.g., to Excel).
- Awaits for the database to finish up before exporting

public async Task<List<Submission>> ExportSubmissionsAsync()
{
    return await _db.Submissions
        .AsNoTracking()
        .OrderByDescending(s => s.SubmittedAt)
        .ToListAsync();
}
## Favourite Punk, Emo, or Hard Rock band

- **Guns N' Roses** 
