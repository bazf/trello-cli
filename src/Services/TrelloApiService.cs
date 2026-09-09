using System.Text.Json;
using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService : IAuthenticationChecker
{
    private readonly HttpClient _http;
    private readonly ConfigService _config;
    private const string ProductionBaseUrl = "https://api.trello.com/1";
    private readonly string _baseUrl;
    private readonly Uri _baseUri;
    private const string HttpRequestFailedMessage = "HTTP request failed.";
    private const string UnexpectedErrorMessage = "Unexpected error occurred.";

    public TrelloApiService(ConfigService config)
        : this(config, CreateProductionHttpClient(), ProductionBaseUrl)
    {
    }

    public TrelloApiService(ConfigService config, HttpClient http)
        : this(config, http, ProductionBaseUrl)
    {
    }

    internal TrelloApiService(ConfigService config, HttpClient http, string baseUrl)
    {
        _config = config;
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _baseUri = new Uri(_baseUrl, UriKind.Absolute);
    }

    private string BuildUrl(string endpoint, string? extraParams = null)
    {
        if (string.IsNullOrEmpty(extraParams)) return $"{_baseUrl}{endpoint}";
        var separator = endpoint.Contains('?') ? "&" : "?";
        return $"{_baseUrl}{endpoint}{separator}{extraParams}";
    }

    internal static HttpClient CreateProductionHttpClient() => new(
        new HttpClientHandler { AllowAutoRedirect = false });

    private async Task<string> GetStringAsync(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // Reports the transport-level status without echoing exception text, which can
    // carry request details, so callers can still distinguish 401 from 429 or 5xx.
    private static string DescribeHttpFailure(HttpRequestException exception) =>
        exception.StatusCode is { } statusCode
            ? $"HTTP request failed with status {(int)statusCode}."
            : HttpRequestFailedMessage;

    private string AuthorizationHeaderValue() =>
        $"OAuth oauth_consumer_key=\"{_config.ApiKey}\", oauth_token=\"{_config.Token}\"";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", AuthorizationHeaderValue());
        return await _http.SendAsync(request);
    }

    // Every endpoint reports failure the same three ways. notFoundMessage is nullable on
    // purpose: several endpoints deliberately let a 404 fall through as HTTP_ERROR, and the
    // error-code table documents that, so it cannot be applied uniformly.
    private async Task<ApiResponse<T>> ExecuteAsync<T>(
        Func<Task<ApiResponse<T>>> operation,
        string? notFoundMessage = null)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException exception)
            when (notFoundMessage is not null && exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return ApiResponse<T>.Fail(notFoundMessage, "NOT_FOUND");
        }
        catch (HttpRequestException exception)
        {
            return ApiResponse<T>.Fail(DescribeHttpFailure(exception), "HTTP_ERROR");
        }
        catch (Exception)
        {
            return ApiResponse<T>.Fail(UnexpectedErrorMessage, "ERROR");
        }
    }

    /// <summary>GET a collection; an empty body is an empty collection, not a failure.</summary>
    private Task<ApiResponse<List<T>>> GetListAsync<T>(string url, string? notFoundMessage) =>
        ExecuteAsync(async () =>
        {
            var body = await GetStringAsync(url);
            return ApiResponse<List<T>>.Success(JsonSerializer.Deserialize<List<T>>(body) ?? new());
        }, notFoundMessage);

    /// <summary>GET a single resource.</summary>
    private Task<ApiResponse<T>> GetObjectAsync<T>(
        string url,
        string missingMessage,
        string missingCode,
        string? notFoundMessage) where T : class =>
        ExecuteAsync(async () =>
        {
            var body = await GetStringAsync(url);
            var value = JsonSerializer.Deserialize<T>(body);
            return value is not null
                ? ApiResponse<T>.Success(value)
                : ApiResponse<T>.Fail(missingMessage, missingCode);
        }, notFoundMessage);

    /// <summary>Send a request and read the resource Trello returns.</summary>
    private Task<ApiResponse<T>> SendForObjectAsync<T>(
        HttpMethod method,
        string url,
        HttpContent? content,
        string failureMessage,
        string failureCode,
        string? notFoundMessage) where T : class =>
        ExecuteAsync(async () =>
        {
            var response = await SendAsync(method, url, content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var value = JsonSerializer.Deserialize<T>(body);
            return value is not null
                ? ApiResponse<T>.Success(value)
                : ApiResponse<T>.Fail(failureMessage, failureCode);
        }, notFoundMessage);

    /// <summary>Send a request and read the collection Trello returns.</summary>
    private Task<ApiResponse<List<T>>> SendForListAsync<T>(
        HttpMethod method,
        string url,
        HttpContent? content,
        string? notFoundMessage) =>
        ExecuteAsync(async () =>
        {
            var response = await SendAsync(method, url, content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            return ApiResponse<List<T>>.Success(JsonSerializer.Deserialize<List<T>>(body) ?? new());
        }, notFoundMessage);

    /// <summary>Send a request whose response body carries nothing worth reading.</summary>
    private Task<ApiResponse<bool>> SendForSuccessAsync(
        HttpMethod method,
        string url,
        string? notFoundMessage,
        HttpContent? content = null) =>
        ExecuteAsync(async () =>
        {
            var response = await SendAsync(method, url, content);
            response.EnsureSuccessStatusCode();
            return ApiResponse<bool>.Success(true);
        }, notFoundMessage);

    // Auth check keeps its own handling: it is the only endpoint that maps 401 to a distinct
    // code, and it projects a fixed shape rather than deserializing a model.
    public async Task<ApiResponse<object>> CheckAuthAsync()
    {
        try
        {
            var url = BuildUrl("/members/me", "fields=id,username,fullName");
            var response = await GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);
            return ApiResponse<object>.Success(new
            {
                id = data.GetProperty("id").GetString(),
                username = data.GetProperty("username").GetString(),
                fullName = data.GetProperty("fullName").GetString()
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return ApiResponse<object>.Fail("Invalid API key or token", "UNAUTHORIZED");
        }
        catch (HttpRequestException exception)
        {
            return ApiResponse<object>.Fail(DescribeHttpFailure(exception), "HTTP_ERROR");
        }
        catch (Exception)
        {
            return ApiResponse<object>.Fail(UnexpectedErrorMessage, "ERROR");
        }
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }
}
