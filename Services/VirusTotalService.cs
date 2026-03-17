using System.Net.Http.Headers;
using System.Text.Json;

namespace VirusDetectionApp.Services;

public class VirusTotalService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

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

    public async Task<(string Status, int Malicious, int Harmless)> GetAnalysisAsync(string analysisId)
    {
        var response = await _httpClient.GetAsync($"analyses/{analysisId}");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"VirusTotal analysis lookup failed: {json}");
        }

        using var document = JsonDocument.Parse(json);

        var attributes = document.RootElement
            .GetProperty("data")
            .GetProperty("attributes");

        var vtStatus = attributes.GetProperty("status").GetString() ?? "unknown";

        int malicious = 0;
        int harmless = 0;

        if (attributes.TryGetProperty("stats", out var stats))
        {
            if (stats.TryGetProperty("malicious", out var maliciousProp))
            {
                malicious = maliciousProp.GetInt32();
            }

            if (stats.TryGetProperty("harmless", out var harmlessProp))
            {
                harmless = harmlessProp.GetInt32();
            }
        }

        var appStatus = vtStatus switch
        {
            "queued" => "Queued",
            "in-progress" => "In Progress",
            "completed" => "Completed",
            _ => "Failed"
        };

        return (appStatus, malicious, harmless);
    }

    public async Task<string> TestConnectionAsync()
    {
        var response = await _httpClient.GetAsync("users/me");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"VirusTotal connection test failed: {json}");
        }

        return json;
    }
}