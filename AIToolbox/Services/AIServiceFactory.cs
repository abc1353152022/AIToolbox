using AIToolbox.Models;
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
        GenericHttp,  // 添加通用HTTP提供商支持
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
    /// 创建通用HTTP服务实例
    /// </summary>
    public static GenericHttpService CreateGenericHttp(HttpClient httpClient, GenericProviderConfig config)
    {
        return new GenericHttpService(httpClient, config);
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

            "generic-http" => CreateGenericHttpService(httpClient, config),

            _ => throw new NotSupportedException($"提供商 '{provider}' 暂不支持")
        };
    }

    private static GenericHttpService CreateGenericHttpService(HttpClient httpClient, Dictionary<string, string> config)
    {
        // For generic-http, we expect the configuration to be more complex
        // In a full implementation, we would deserialize the config into GenericProviderConfig
        // For now, we'll create a basic config from the simple dictionary
        var genericConfig = new GenericProviderConfig
        {
            BaseUrl = config.GetValueOrDefault("baseUrl") ?? string.Empty,
            ApiKey = config.GetValueOrDefault("apiKey"),
            SupportsStreaming = bool.Parse(config.GetValueOrDefault("supportsStreaming") ?? "true")
        };

        // Handle endpoints if provided
        if (config.ContainsKey("endpointsChat") || config.ContainsKey("endpointsStreaming") || config.ContainsKey("endpointsModels"))
        {
            genericConfig.Endpoints = new GenericProviderEndpoints
            {
                Chat = config.GetValueOrDefault("endpointsChat") ?? "/v1/chat/completions",
                Streaming = config.GetValueOrDefault("endpointsStreaming") ?? "/v1/chat/completions",
                Models = config.GetValueOrDefault("endpointsModels") ?? "/v1/models"
            };
        }

        return new GenericHttpService(httpClient, genericConfig);
    }
}