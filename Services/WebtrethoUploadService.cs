using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageUploadApp.Models;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Services;

public class WebtrethoUploadService : IWebtrethoUploadService
{
    private readonly HttpClient _http;
    private readonly WebtrethoOptions _options;

    public WebtrethoUploadService(HttpClient http, IOptions<WebtrethoOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<WebtrethoUploadResult?> UploadJpegAsync(byte[] jpegBytes, string fileName, CancellationToken cancellationToken = default)
    {
        var query =
            $"mutation ($file: Upload!) {{ uploadFile(file: $file, fileType: PostContent, createdBy: \"{_options.CreatedBy}\") {{ id mimeType fileExtension encoding contentType originalUrl cdnUrl uri isDeleted createdAt createdBy }} }}";
        var operationsObj = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["variables"] = new Dictionary<string, object?> { ["file"] = null },
        };
        var operations = JsonSerializer.Serialize(operationsObj, SerializerOptions);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(operations, Encoding.UTF8, "application/json"), "operations");
        multipart.Add(new StringContent("""{"0":["variables.file"]}""", Encoding.UTF8, "application/json"), "map");

        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(fileContent, "0", string.IsNullOrWhiteSpace(fileName) ? "upload.jpg" : fileName);

        using var response = await _http.PostAsync(_options.ApiUrl, multipart, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var parsed = JsonSerializer.Deserialize<WebtrethoUploadResponse>(json, SerializerOptions);
        var file = parsed?.Data?.UploadFile;
        if (file?.CdnUrl is not { Length: > 0 } cdn)
            return null;

        return new WebtrethoUploadResult(cdn, file.Id);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class WebtrethoUploadResponse
    {
        [JsonPropertyName("data")]
        public DataNode? Data { get; set; }
    }

    private sealed class DataNode
    {
        [JsonPropertyName("uploadFile")]
        public FileNode? UploadFile { get; set; }
    }

    private sealed class FileNode
    {
        [JsonPropertyName("cdnUrl")]
        public string? CdnUrl { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
