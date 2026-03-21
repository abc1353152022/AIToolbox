using Microsoft.Extensions.DependencyInjection;

namespace AIToolbox.Services;

/// <summary>
/// AI 服务工厂 - 支持未来扩展
/// </summary>
public static class AIServiceFactory
{
    /// <summary>
    /// 支持的提供商类型
    /// </summary>
    public enum ProviderType
    {
        Ollama,
        Aitools,
        DeepSeek,
        OpenAI,
        // Anthropic,  // 未来添加
    }

    /// <summary>
    /// 创建 Ollama 服务实例
    /// </summary>
    public static OllamaService CreateOllama(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
    {
        return new OllamaService(httpClient, baseUrl, apiKey);
    }

    /// <summary>
    /// 创建 Aitools 服务实例
    /// </summary>
    public static AitoolsService CreateAitools(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
    {
        return new AitoolsService(httpClient, baseUrl, apiKey);
    }

    /// <summary>
    /// 创建 DeepSeek 服务实例
    /// </summary>
    public static DeepSeekService CreateDeepSeek(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
    {
        return new DeepSeekService(httpClient, baseUrl, apiKey);
    }

    /// <summary>
    /// 创建 OpenAI 服务实例
    /// </summary>
    public static OpenAIService CreateOpenAI(HttpClient httpClient, string? baseUrl = null, string? apiKey = null)
    {
        return new OpenAIService(httpClient, baseUrl, apiKey);
    }

    /// <summary>
    /// 根据配置创建服务
    /// </summary>
    public static IAIService Create(HttpClient httpClient, string provider, Dictionary<string, string> config)
    {
        return provider.ToLower() switch
        {
            "ollama" => new OllamaService(
                httpClient,
                config.GetValueOrDefault("baseUrl"),
                config.GetValueOrDefault("apiKey")),

            "aitools" => new AitoolsService(
                httpClient,
                config.GetValueOrDefault("baseUrl"),
                config.GetValueOrDefault("apiKey")),

            "deepseek" => new DeepSeekService(
                httpClient,
                config.GetValueOrDefault("baseUrl"),
                config.GetValueOrDefault("apiKey")),

            "openai" => new OpenAIService(
                httpClient,
                config.GetValueOrDefault("baseUrl"),
                config.GetValueOrDefault("apiKey")),

            _ => throw new NotSupportedException($"提供商 '{provider}' 暂不支持")
        };
    }
}