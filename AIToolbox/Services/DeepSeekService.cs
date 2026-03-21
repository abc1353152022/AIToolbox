using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIToolbox.Models;

namespace AIToolbox.Services;

public class DeepSeekService : BaseAIService
{
    private const string DEFAULT_BASE_URL = "https://api.deepseek.com";
    private const string CHAT_ENDPOINT = "/v1/chat/completions";

    public DeepSeekService(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
        : base(httpClient, baseUrl ?? DEFAULT_BASE_URL, apiKey)
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
            stream = false
        };

        var (responseJson, error) = await PostRequestAsync(CHAT_ENDPOINT, request);

        if (error != null)
            return (null, error);

        try
        {
            var deepseekResponse = JsonSerializer.Deserialize<DeepSeekChatResponse>(responseJson!, _jsonOptions);
            if (deepseekResponse == null)
                return (null, "响应解析失败");

            var response = new ChatResponse
            {
                Model = deepseekResponse.Model ?? model,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(deepseekResponse.Created).ToString("o"),
                Done = true,
                TotalDuration = 0,
                PromptEvalCount = deepseekResponse.Usage?.PromptTokens ?? 0,
                EvalCount = deepseekResponse.Usage?.CompletionTokens ?? 0,
                Message = deepseekResponse.Choices?.FirstOrDefault()?.Message
            };

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
            stream = true
        };

        await foreach (var chunk in ProcessStreamResponseAsync<DeepSeekStreamChunk>(
            CHAT_ENDPOINT,
            request,
            ParseStreamChunk,
            cancellationToken))
        {
            if (chunk != null)
            {
                yield return new StreamChunk
                {
                    Content = chunk.Choices?.FirstOrDefault()?.Delta?.Content ?? "",
                    Done = chunk.Choices?.FirstOrDefault()?.FinishReason == "stop",
                    PromptEvalCount = chunk.Usage?.PromptTokens,
                    EvalCount = chunk.Usage?.CompletionTokens,
                    TotalDuration = null
                };
            }
        }
    }

    private DeepSeekStreamChunk? ParseStreamChunk(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DeepSeekStreamChunk>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public override Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        var models = new List<ModelInfo>
        {
            new() { Id = "deepseek-chat", Name = "DeepSeek Chat", Description = "DeepSeek 对话模型", Provider = "DeepSeek", Recommended = true, IsFree = false },
            new() { Id = "deepseek-coder", Name = "DeepSeek Coder", Description = "DeepSeek 编程模型", Provider = "DeepSeek", Recommended = false, IsFree = false },
            new() { Id = "deepseek-reasoner", Name = "DeepSeek Reasoner", Description = "DeepSeek 推理模型", Provider = "DeepSeek", Recommended = true, IsFree = false }
        };

        return Task.FromResult(models);
    }

    public override ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Provider = "DeepSeek",
            Version = "1.0",
            RequiresApiKey = true,
            SupportsStreaming = true,
            Capabilities = new List<string> { "chat", "generate", "coding", "streaming" }
        };
    }

    public override async Task<bool> TestConnectionAsync()
    {
        var request = new { model = "test", messages = new[] { new { role = "user", content = "test" } } };
        var (_, error) = await PostRequestAsync(CHAT_ENDPOINT, request);
        if (error != null && (error.Contains("500") || error.Contains("401") || error.Contains("402") || error.Contains("400")))
            return true;
        return error == null;
    }
}

// DeepSeek API 响应类型
internal class DeepSeekChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<DeepSeekChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

internal class DeepSeekChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MessageResponse? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// 流式响应
internal class DeepSeekStreamChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<DeepSeekStreamChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

internal class DeepSeekStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public DeepSeekDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class DeepSeekDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
