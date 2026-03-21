using AIToolbox.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIToolbox.Services;

public class OllamaService : BaseAIService
{
    private const string DEFAULT_URL = "http://localhost:11434";
    private const string CLOUD_URL = "https://api.ollama.com";
    private const string CHAT_ENDPOINT = "/api/chat";
    private const string TAGS_ENDPOINT = "/api/tags";

    public OllamaService(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
        : base(httpClient, baseUrl ?? DEFAULT_URL, apiKey)
    {
    }

    public override async Task<(ChatResponse? Response, string? Error)> SendMessageAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null)
    {
        var request = new
        {
            model,
            messages,
            stream = false,
            options = options ?? new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["top_p"] = 0.9
            }
        };

        var (responseJson, error) = await PostRequestAsync(CHAT_ENDPOINT, request);

        if (error != null)
            return (null, error);

        try
        {
            var response = JsonSerializer.Deserialize<ChatResponse>(responseJson!);
            return (response, null);
        }
        catch (JsonException ex)
        {
            return (null, $"解析失败: {ex.Message}");
        }
    }

    public override async IAsyncEnumerable<StreamChunk> SendMessageStreamAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model,
            messages,
            stream = true,  // 启用流式
            options = options ?? new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["top_p"] = 0.9
            }
        };

        await foreach (var chunk in ProcessStreamResponseAsync<OllamaStreamChunk>(
            CHAT_ENDPOINT,
            request,
            ParseStreamChunk,
            cancellationToken))
        {
            if (chunk != null)
            {
                yield return new StreamChunk
                {
                    Content = chunk.Message?.Content ?? "",
                    Done = chunk.Done,
                    PromptEvalCount = chunk.PromptEvalCount,
                    EvalCount = chunk.EvalCount,
                    TotalDuration = chunk.TotalDuration
                };
            }
        }
    }

    private OllamaStreamChunk? ParseStreamChunk(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OllamaStreamChunk>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public override async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        var models = new List<ModelInfo>();

        try
        {
            var (responseJson, error) = await GetRequestAsync(TAGS_ENDPOINT);
            if (error == null && responseJson != null)
            {
                var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var item in modelsArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var name))
                        {
                            models.Add(new ModelInfo
                            {
                                Id = name.GetString() ?? "",
                                Name = name.GetString() ?? "",
                                Description = "已安装的模型",
                                Provider = "Ollama",
                                Recommended = false
                            });
                        }
                    }
                }
            }
        }
        catch { }

        models.AddRange(GetRecommendedModels());

        return models;
    }

    private List<ModelInfo> GetRecommendedModels()
    {
        return new List<ModelInfo>
        {
            new ModelInfo
            {
                Id = "llama3.2",
                Name = "Llama 3.2",
                Description = "Meta 最新轻量级模型，速度快",
                Provider = "Meta",
                ContextLength = 131072,
                Recommended = true,
                IsFree = true,
                Size = "1B/3B",
                Languages = new List<string> { "en", "zh", "es", "fr", "de" },
                Capabilities = new List<string> { "chat", "text-generation" },
                Tags = new List<string> { "fast", "lightweight" }
            },
            new ModelInfo
            {
                Id = "qwen2.5",
                Name = "Qwen 2.5",
                Description = "阿里千问系列，中文理解能力强",
                Provider = "Alibaba",
                ContextLength = 131072,
                Recommended = true,
                IsFree = true,
                Size = "0.5B-72B",
                Languages = new List<string> { "zh", "en" },
                Capabilities = new List<string> { "chat", "text-generation", "coding" },
                Tags = new List<string> { "chinese", "coding" }
            },
            new ModelInfo
            {
                Id = "deepseek-r1",
                Name = "DeepSeek R1",
                Description = "DeepSeek 推理增强模型",
                Provider = "DeepSeek",
                ContextLength = 131072,
                Recommended = true,
                IsFree = true,
                Size = "1.5B-70B",
                Languages = new List<string> { "zh", "en" },
                Capabilities = new List<string> { "chat", "reasoning", "coding" },
                Tags = new List<string> { "reasoning", "math" }
            },
            new ModelInfo
            {
                Id = "gemma2",
                Name = "Gemma 2",
                Description = "Google 开源模型",
                Provider = "Google",
                ContextLength = 8192,
                Recommended = true,
                IsFree = true,
                Size = "2B/9B/27B",
                Languages = new List<string> { "en" },
                Capabilities = new List<string> { "chat", "text-generation" },
                Tags = new List<string> { "google", "safe" }
            }
        };
    }

    public override ServiceInfo GetServiceInfo()
    {
        var isCloud = _baseUrl == CLOUD_URL;

        return new ServiceInfo
        {
            Provider = "Ollama",
            Version = "1.0",
            RequiresApiKey = isCloud,
            SupportsStreaming = true,
            Capabilities = new List<string> { "chat", "generate", "embeddings", "streaming" }
        };
    }

    public override async Task<bool> TestConnectionAsync()
    {
        var (_, error) = await GetRequestAsync(TAGS_ENDPOINT);
        return error == null;
    }

    public void UseCloudMode(string apiKey)
    {
        SetBaseUrl(CLOUD_URL);
        SetApiKey(apiKey);
    }

    public void UseLocalMode(string? localUrl = null)
    {
        SetBaseUrl(localUrl ?? DEFAULT_URL);
        SetApiKey(null);
    }
}

/// <summary>
/// Ollama 流式响应块
/// </summary>
public class OllamaStreamChunk
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public MessageResponse? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }

    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
}