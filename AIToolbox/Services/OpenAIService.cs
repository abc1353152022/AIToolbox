using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIToolbox.Models;

namespace AIToolbox.Services;

public class OpenAIService : BaseAIService
{
    private const string DEFAULT_BASE_URL = "https://api.openai.com";
    private const string CHAT_ENDPOINT = "/v1/chat/completions";

    public OpenAIService(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
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
            var openaiResponse = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson!, _jsonOptions);
            if (openaiResponse == null)
                return (null, "响应解析失败");

            var response = new ChatResponse
            {
                Model = openaiResponse.Model ?? model,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(openaiResponse.Created).ToString("o"),
                Done = true,
                TotalDuration = 0,
                PromptEvalCount = openaiResponse.Usage?.PromptTokens ?? 0,
                EvalCount = openaiResponse.Usage?.CompletionTokens ?? 0,
                Message = openaiResponse.Choices?.FirstOrDefault()?.Message
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

        await foreach (var chunk in ProcessStreamResponseAsync<OpenAIStreamChunk>(
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

    private OpenAIStreamChunk? ParseStreamChunk(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OpenAIStreamChunk>(json, _jsonOptions);
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
            new() { Id = "gpt-4o", Name = "GPT-4o", Description = "OpenAI 最新多模态模型", Provider = "OpenAI", Recommended = true, IsFree = false },
            new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Description = "OpenAI 轻量快速模型", Provider = "OpenAI", Recommended = false, IsFree = false },
            new() { Id = "gpt-4-turbo", Name = "GPT-4 Turbo", Description = "GPT-4 增强版", Provider = "OpenAI", Recommended = false, IsFree = false },
            new() { Id = "gpt-3.5-turbo", Name = "GPT-3.5 Turbo", Description = "快速经济的模型", Provider = "OpenAI", Recommended = false, IsFree = false },
            new() { Id = "o1-mini", Name = "o1 Mini", Description = "OpenAI 推理轻量模型", Provider = "OpenAI", Recommended = false, IsFree = false },
            new() { Id = "o1-preview", Name = "o1 Preview", Description = "OpenAI 推理模型", Provider = "OpenAI", Recommended = false, IsFree = false }
        };

        return Task.FromResult(models);
    }

    public override ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Provider = "OpenAI",
            Version = "1.0",
            RequiresApiKey = true,
            SupportsStreaming = true,
            Capabilities = new List<string> { "chat", "generate", "vision", "streaming" }
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

// OpenAI API 响应类型
internal class OpenAIChatResponse
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
    public List<OpenAIChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

internal class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MessageResponse? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// 流式响应
internal class OpenAIStreamChunk
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
    public List<OpenAIStreamChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

internal class OpenAIStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public OpenAIDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenAIDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
