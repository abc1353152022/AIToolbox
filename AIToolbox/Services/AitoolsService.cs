using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIToolbox.Models;

namespace AIToolbox.Services;

public class AitoolsService : BaseAIService
{
    private const string DEFAULT_BASE_URL = "https://platform.aitools.cfd";
    private const string CHAT_ENDPOINT = "/api/v1/chat/completions";

    public AitoolsService(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
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
            var aitoolsResponse = JsonSerializer.Deserialize<AitoolsChatResponse>(responseJson!, _jsonOptions);
            if (aitoolsResponse == null)
                return (null, "响应解析失败");

            var response = new ChatResponse
            {
                Model = aitoolsResponse.Model ?? model,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(aitoolsResponse.Created).ToString("o"),
                Done = true,
                TotalDuration = 0,
                PromptEvalCount = aitoolsResponse.Usage?.PromptTokens ?? 0,
                EvalCount = aitoolsResponse.Usage?.CompletionTokens ?? 0,
                Message = aitoolsResponse.Choices?.FirstOrDefault()?.Message
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

        await foreach (var chunk in ProcessStreamResponseAsync<AitoolsStreamChunk>(
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

    private AitoolsStreamChunk? ParseStreamChunk(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AitoolsStreamChunk>(json, _jsonOptions);
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
            new() { Id = "deepseek/deepseek-r1-0528", Name = "DeepSeek R1 0528", Description = "DeepSeek 推理增强模型", Provider = "DeepSeek", Recommended = true, IsFree = false },
            new() { Id = "deepseek/deepseek-v3-0324", Name = "DeepSeek V3 0324", Description = "DeepSeek 最新模型", Provider = "DeepSeek", Recommended = true, IsFree = false },
            new() { Id = "google/gemini-2.0-flash-exp", Name = "Gemini 2.0 Flash", Description = "Google 最新快速模型", Provider = "Google", Recommended = true, IsFree = false },
            new() { Id = "google/gemma-3-27b", Name = "Gemma 3 27B", Description = "Google 开源模型", Provider = "Google", Recommended = false, IsFree = false },
            new() { Id = "qwen/qwen2.5-7b", Name = "Qwen 2.5 7B", Description = "阿里千问轻量版", Provider = "Alibaba", Recommended = false, IsFree = false },
            new() { Id = "qwen/qwen2.5-72b", Name = "Qwen 2.5 72B", Description = "阿里千问旗舰版", Provider = "Alibaba", Recommended = true, IsFree = false },
            new() { Id = "qwen/qwen2.5-vl-32b", Name = "Qwen 2.5 VL 32B", Description = "千问视觉模型", Provider = "Alibaba", Recommended = false, IsFree = false },
            new() { Id = "qwen/qwen3-8b", Name = "Qwen 3 8B", Description = "阿里千问新一代", Provider = "Alibaba", Recommended = true, IsFree = false },
            new() { Id = "qwen/qwen3-30b-a3b", Name = "Qwen 3 30B A3B", Description = "千问 MoE 模型", Provider = "Alibaba", Recommended = false, IsFree = false },
            new() { Id = "qwen/qwen3-14b", Name = "Qwen 3 14B", Description = "千问中等规模", Provider = "Alibaba", Recommended = false, IsFree = false },
            new() { Id = "qwen/qwen3-coder", Name = "Qwen 3 Coder", Description = "千问编程模型", Provider = "Alibaba", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4-9b", Name = "GLM-4 9B", Description = "智谱 GLM-4 基础版", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4-flash", Name = "GLM-4 Flash", Description = "智谱快速版本", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4v-flash", Name = "GLM-4V Flash", Description = "智谱视觉模型", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4.1v-thinking-flash", Name = "GLM-4.1V Thinking Flash", Description = "智谱思考模型", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4.5-flash", Name = "GLM-4.5 Flash", Description = "智谱新版快速模型", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "zhipu/glm-4.6v-flash", Name = "GLM-4.6V Flash", Description = "智谱新版视觉模型", Provider = "Zhipu", Recommended = false, IsFree = false },
            new() { Id = "openai/gpt-oss-20b", Name = "GPT OSS 20B", Description = "开源 GPT 模型", Provider = "OpenAI", Recommended = false, IsFree = false },
            new() { Id = "xiaomi/mimo-v2-flash", Name = "Mimo V2 Flash", Description = "小米模型", Provider = "Xiaomi", Recommended = false, IsFree = false }
        };

        return Task.FromResult(models);
    }

    public override ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Provider = "AITools",
            Version = "1.0",
            RequiresApiKey = true,
            SupportsStreaming = true,
            Capabilities = new List<string> { "chat", "generate", "streaming" }
        };
    }

    public override async Task<bool> TestConnectionAsync()
    {
        // 简单测试：用无效模型测试 API 是否可达
        var request = new { model = "test", messages = new[] { new { role = "user", content = "test" } } };
        var (_, error) = await PostRequestAsync(CHAT_ENDPOINT, request);

        // 只要服务器有响应（任何状态码）就说明 API 可达
        // 500 + "Unsupported model" 表示 API 正常工作，只是模型不支持
        // 401 表示密钥问题，但这也是 API 可达的意思
        if (error != null && (error.Contains("500") || error.Contains("401") || error.Contains("402")))
            return true;
        return error == null;
    }
}

// Aitools API 响应类型
internal class AitoolsChatResponse
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
    public List<AitoolsChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public AitoolsUsage? Usage { get; set; }
}

internal class AitoolsChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MessageResponse? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class AitoolsUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// 流式响应
internal class AitoolsStreamChunk
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
    public List<AitoolsStreamChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public AitoolsUsage? Usage { get; set; }
}

internal class AitoolsStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AitoolsDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class AitoolsDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
