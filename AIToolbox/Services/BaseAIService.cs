using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using AIToolbox.Models;

namespace AIToolbox.Services;

/// <summary>
/// AI 服务基类，提供通用功能
/// </summary>
public abstract class BaseAIService : IAIService
{
    protected readonly HttpClient _httpClient;
    protected string _baseUrl;
    protected string? _apiKey;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected BaseAIService(HttpClient httpClient, string baseUrl, string? apiKey = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    public abstract Task<(ChatResponse? Response, string? Error)> SendMessageAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null);

    public abstract IAsyncEnumerable<StreamChunk> SendMessageStreamAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ModelInfo>> GetAvailableModelsAsync();

    public abstract ServiceInfo GetServiceInfo();

    public abstract Task<bool> TestConnectionAsync();

    public virtual void UpdateConfig(Dictionary<string, string> config)
    {
        if (config.TryGetValue("ApiKey", out var apiKey))
        {
            SetApiKey(apiKey);
        }

        if (config.TryGetValue("BaseUrl", out var baseUrl))
        {
            SetBaseUrl(baseUrl);
        }
    }

    protected void SetApiKey(string? apiKey)
    {
        _apiKey = apiKey;
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    protected void SetBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    protected async Task<(string? Response, string? Error)> PostRequestAsync(
        string endpoint,
        object requestBody)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_baseUrl}{endpoint}";

            var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {response.StatusCode}: {responseJson}");
            }

            return (responseJson, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    protected async Task<(string? Response, string? Error)> GetRequestAsync(string endpoint)
    {
        try
        {
            var url = $"{_baseUrl}{endpoint}";
            var response = await _httpClient.GetAsync(url);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {response.StatusCode}: {responseJson}");
            }

            return (responseJson, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// 处理流式响应
    /// </summary>
    protected async IAsyncEnumerable<T> ProcessStreamResponseAsync<T>(
        string endpoint,
        object requestBody,
        Func<string, T?> parser,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_baseUrl}{endpoint}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE 格式: "data: {...}"
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);

                    // 检查结束标记
                    if (data == "[DONE]")
                    {
                        yield break;
                    }

                    var parsed = parser(data);
                    if (parsed != null)
                    {
                        yield return parsed;
                    }
                }
                // 某些 API 可能直接返回 JSON
                else if (line.StartsWith("{"))
                {
                    var parsed = parser(line);
                    if (parsed != null)
                    {
                        yield return parsed;
                    }
                }
            }
    }
}