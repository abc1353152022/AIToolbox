using System.Collections.Generic;

namespace AIToolbox.Models;

/// <summary>
/// Configuration model for generic HTTP AI providers
/// </summary>
public class GenericProviderConfig
{
    /// <summary>
    /// Base URL for the API service
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API endpoint configuration
    /// </summary>
    public GenericProviderEndpoints Endpoints { get; set; } = new();

    /// <summary>
    /// Authentication configuration
    /// </summary>
    public GenericProviderAuthentication Authentication { get; set; } = new();

    /// <summary>
    /// Request transformation mapping
    /// </summary>
    public GenericProviderRequestMapping RequestMapping { get; set; } = new();

    /// <summary>
    /// Response transformation mapping
    /// </summary>
    public GenericProviderResponseMapping ResponseMapping { get; set; } = new();

    /// <summary>
    /// Streaming response transformation mapping
    /// </summary>
    public GenericProviderStreamingResponseMapping StreamingResponseMapping { get; set; } = new();

    /// <summary>
    /// Whether the provider supports streaming
    /// </summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>
    /// Additional headers to include in requests
    /// </summary>
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();
}

/// <summary>
/// Endpoint configuration for generic provider
/// </summary>
public class GenericProviderEndpoints
{
    /// <summary>
    /// Chat completion endpoint
    /// </summary>
    public string Chat { get; set; } = "/v1/chat/completions";

    /// <summary>
    /// Streaming chat completion endpoint (if different from chat)
    /// </summary>
    public string Streaming { get; set; } = "/v1/chat/completions";

    /// <summary>
    /// Models listing endpoint
    /// </summary>
    public string Models { get; set; } = "/v1/models";
}

/// <summary>
/// Authentication configuration for generic provider
/// </summary>
public class GenericProviderAuthentication
{
    /// <summary>
    /// Authentication type (bearer, apiKey, none)
    /// </summary>
    public string Type { get; set; } = "bearer";

    /// <summary>
    /// Header name for API key (when Type is apiKey)
    /// </summary>
    public string Header { get; set; } = "Authorization";

    /// <summary>
    /// Key prefix for API key header (e.g., "Bearer " or empty)
    /// </summary>
    public string Prefix { get; set; } = "Bearer ";
}

/// <summary>
/// Request transformation mapping for generic provider
/// </summary>
public class GenericProviderRequestMapping
{
    /// <summary>
    /// Mapping for model ID
    /// </summary>
    public string Model { get; set; } = "model";

    /// <summary>
    /// Mapping for messages array
    /// </summary>
    public string Messages { get; set; } = "messages";

    /// <summary>
    /// Mapping for temperature parameter
    /// </summary>
    public string? Temperature { get; set; } = "temperature";

    /// <summary>
    /// Mapping for top_p parameter
    /// </summary>
    public string? TopP { get; set; } = "top_p";

    /// <summary>
    /// Mapping for max tokens parameter
    /// </summary>
    public string? MaxTokens { get; set; } = "max_tokens";

    /// <summary>
    /// Mapping for stream parameter
    /// </summary>
    public string Stream { get; set; } = "stream";

    /// <summary>
    /// Additional custom mappings (JSON path -> value expression)
    /// </summary>
    public Dictionary<string, string> AdditionalMappings { get; set; } = new();
}

/// <summary>
/// Response transformation mapping for generic provider
/// </summary>
public class GenericProviderResponseMapping
{
    /// <summary>
    /// Mapping for response ID
    /// </>
    public string Id { get; set; } = "id";

    /// <summary>
    /// Mapping for creation timestamp
    /// </summary>
    public string CreatedAt { get; set; } = "created";

    /// <summary>
    /// Mapping for model ID
    /// </summary>
    public string Model { get; set; } = "model";

    /// <summary>
    /// Mapping for message content
    /// </summary>
    public GenericProviderMessageMapping Message { get; set; } = new();

    /// <summary>
    /// Mapping for done/completion flag
    /// </summary>
    public string Done { get; set; } = "done";

    /// <summary>
    /// Mapping for usage information
    /// </summary>
    public GenericProviderUsageMapping Usage { get; set; } = new();
}

/// <summary>
/// Message mapping within response
/// </summary>
public class GenericProviderMessageMapping
{
    /// <summary>
    /// Mapping for message role
    /// </summary>
    public string Role { get; set; } = "role";

    /// <summary>
    /// Mapping for message content
    /// </summary>
    public string Content { get; set; } = "content";
}

/// <summary>
/// Usage mapping within response
/// </summary>
public class GenericProviderUsageMapping
{
    /// <summary>
    /// Mapping for prompt tokens
    /// </summary>
    public string PromptTokens { get; set; } = "prompt_tokens";

    /// <summary>
    /// Mapping for completion tokens
    /// </summary>
    public string CompletionTokens { get; set; } = "completion_tokens";

    /// <summary>
    /// Mapping for total tokens
    /// </summary>
    public string? TotalTokens { get; set; } = "total_tokens";
}

/// <summary>
/// Streaming response transformation mapping for generic provider
/// </summary>
public class GenericProviderStreamingResponseMapping
{
    /// <summary>
    /// Mapping for content delta
    /// </summary>
    public string Content { get; set; } = "content";

    /// <summary>
    /// Mapping for done/completion flag
    /// </summary>
    public string Done { get; set; } = "done";

    /// <summary>
    /// Mapping for usage information in streaming response
    /// </summary>
    public GenericProviderUsageMapping Usage { get; set; } = new();
}