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
- Files are automatically deleted once analysis completes or fails (by the background service)
- File analysis is handled asynchronously via a background service
- Multiple antivirus engines are used by VirusTotal for comprehensive scanning

### Background Processing
- `SubmissionBackgroundService` periodically polls VirusTotal for analysis results
- Submissions are tracked and automatically updated when results become available
- Results can be exported to Excel format for reporting

### Export Functionality
- Submission history can be exported to Excel files using `ExportService`
- Includes submission details, file hashes, and analysis results

### Development vs. Production
- Development logging level is set to "Information"
- ASP.NET Core warnings are suppressed in logging
- HTTPS redirection and HSTS are enabled in production

### Limitations & Considerations
- Free VirusTotal API tier has rate limits; paid plans offer higher quotas
- File analysis completion time varies based on VirusTotal's queue
- Ensure adequate disk space for the SQLite database and uploaded files
- The application uses interactive server components for real-time UI updates

## Troubleshooting

- **Database errors**: Delete `virussubmissions.db` to reset the database
- **API connection issues**: Verify your API key and internet connectivity
- **Port already in use**: The default HTTPS port (5001) may be in use; check with `lsof -i :5001`

## License

[Add appropriate license information here]
