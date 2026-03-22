using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIToolbox.Models;

/// <summary>
/// Application settings model
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Default provider to use when none specified
    /// </summary>
    public string DefaultProvider { get; set; } = "ollama";

    /// <summary>
    /// Provider configurations keyed by provider ID
    /// </summary>
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}

/// <summary>
/// Base provider configuration
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// Base URL for the API service
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// </// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Provider type (ollama, aitools, deepseek, openai, generic-http, etc.)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ollama";

    /// <summary>
    /// Generic provider specific configuration (used when Type is "generic-http")
    /// </summary>
    [JsonPropertyName("generic")]
    public GenericProviderConfig? GenericConfig { get; set; }

    /// <summary>
    /// Endpoint configuration (overrides defaults)
    /// </summary>
    public ProviderEndpoints? Endpoints { get; set; }

    /// <summary>
    /// Additional headers to include in requests
    /// </summary>
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();
}

/// <summary>
/// Endpoint configuration override
/// </summary>
public class ProviderEndpoints
{
    /// <summary>
    /// Chat completion endpoint
    /// </summary>
    public string Chat { get; set; } = string.Empty;

    /// <summary>
    /// Streaming chat completion endpoint
    /// </summary>
    public string Streaming { get; set; } = string.Empty;

    /// <summary>
    /// Models listing endpoint
    /// </summary>
    public string Models { get; set; } = string.Empty;
}