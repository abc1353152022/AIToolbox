using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using AIToolbox.Models;

namespace AIToolbox.Services;

/// <summary>
/// Generic HTTP AI service provider that can be configured to work with various APIs
/// </summary>
public class GenericHttpService : BaseAIService
{
    private readonly GenericProviderConfig _config;

    public GenericHttpService(HttpClient httpClient, GenericProviderConfig config)
        : base(httpClient, config.BaseUrl, config.ApiKey)
    {
        _config = config;

        // Apply additional headers if configured
        if (_config.AdditionalHeaders != null)
        {
            foreach (var header in _config.AdditionalHeaders)
            {
                if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value))
                {
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }
    }

    public override async Task<(ChatResponse? Response, string? Error)> SendMessageAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null)
    {
        if (!_config.SupportsStreaming)
        {
            return await SendMessageNonStreamingAsync(model, messages, options);
        }
        else
        {
            // For providers that support both, we'll use non-streaming for this method
            return await SendMessageNonStreamingAsync(model, messages, options);
        }
    }

    public override async IAsyncEnumerable<StreamChunk> SendMessageStreamAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_config.SupportsStreaming)
        {
            // Fall back to non-streaming if streaming not supported
            var (response, error) = await SendMessageNonStreamingAsync(model, messages, options);
            if (error != null)
            {
                yield return new StreamChunk { Content = $"Error: {error}" };
                yield break;
            }

            if (response?.Message != null)
            {
                yield return new StreamChunk
                {
                    Content = response.Message.Content,
                    Done = true,
                    PromptEvalCount = response.PromptEvalCount,
                    EvalCount = response.EvalCount
                };
                yield break;
            }
        }
        else
        {
            await foreach (var chunk in SendMessageStreamingAsync(model, messages, options, cancellationToken))
            {
                yield return chunk;
            }
        }
    }

    private async Task<(ChatResponse? Response, string? Error)> SendMessageNonStreamingAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null)
    {
        try
        {
            // Transform request
            var request = TransformRequest(model, messages, options);

            // Make HTTP request
            var (responseJson, error) = await PostRequestAsync(_config.Endpoints.Chat, request);

            if (error != null)
            {
                return (null, error);
            }

            if (string.IsNullOrEmpty(responseJson))
            {
                return (null, "Empty response from API");
            }

            // Transform response
            var chatResponse = TransformResponse(responseJson);
            return (chatResponse, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to send message: {ex.Message}");
        }
    }

    private async IAsyncEnumerable<StreamChunk> SendMessageStreamingAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Transform request for streaming
        var request = TransformRequestForStreaming(model, messages, options);

        // Make HTTP request and process stream
        await foreach (var chunk in ProcessStreamResponseAsync<StreamChunk>(
                    _config.Endpoints.Streaming,
                    request,
                    ParseStreamingChunk,
                    cancellationToken))
        {
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    private object TransformRequest(string model, List<Message> messages, Dictionary<string, object>? options = null)
    {
        // Start with base request object
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages
        };

        // Add options if provided
        if (options != null && options.Count > 0)
        {
            foreach (var option in options)
            {
                request[option.Key] = option.Value;
            }
        }

        // Apply request mapping transformations
        return ApplyRequestMapping(request);
    }

    private object TransformRequestForStreaming(string model, List<Message> messages, Dictionary<string, object>? options = null)
    {
        var request = TransformRequest(model, messages, options);

        if (_config.RequestMapping.Stream != null)
        {
            if (options == null)
            {
                options = new Dictionary<string, object>();
            }
            options["stream"] = true;

            // 明确指定类型为 Dictionary<string, object>
            Dictionary<string, object> dictRequest = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = true
            };

            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option.Key != "stream")
                    {
                        dictRequest[option.Key] = option.Value;
                    }
                }
            }

            request = dictRequest;
        }

        return request;
    }

    private object ApplyRequestMapping(object request)
    {
        // For now, we'll pass through the request as-is
        // In a more advanced implementation, we would apply JSON path transformations
        // or use a templating engine to map AIToolbox format to provider format
        //
        // For Phase 1, we'll focus on OpenAI-compatible APIs by default and allow
        // endpoint configuration rather than complex request/response transformation

        // If we detect this should be transformed to a specific format, we would do it here
        // For example, if the provider expects a different structure for messages

        return request;
    }

    private ChatResponse TransformResponse(string json)
    {
        try
        {
            // For Phase 1, we'll assume OpenAI-compatible response format
            // and map it to our ChatResponse format
            // In a full implementation, we would apply response mapping transformations

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract basic fields
            string id = root.GetProperty("id").GetString() ?? string.Empty;
            string model = root.GetProperty("model").GetString() ?? string.Empty;
            string createdAt = root.GetProperty("created").GetInt64().ToString(); // Unix timestamp

            // Extract message
            MessageResponse? message = null;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageProp))
                {
                    string role = messageProp.GetProperty("role").GetString() ?? "assistant";
                    string content = messageProp.GetProperty("content").GetString() ?? string.Empty;
                    message = new MessageResponse { Role = role, Content = content };
                }
            }

            // Extract usage
            int promptTokens = 0;
            int completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt))
                {
                    promptTokens = prompt.GetInt32();
                }
                if (usage.TryGetProperty("completion_tokens", out var completion))
                {
                    completionTokens = completion.GetInt32();
                }
            }

            bool done = true; // Non-streaming is always done
            if (root.TryGetProperty("choices", out var choicesProp) && choicesProp.GetArrayLength() > 0)
            {
                var firstChoice = choicesProp[0];
                if (firstChoice.TryGetProperty("finish_reason", out var finishReason))
                {
                    done = finishKindToBoolean(finishReason.GetString());
                }
            }

            return new ChatResponse
            {
                Id = id,
                Model = model,
                CreatedAt = createdAt,
                Message = message,
                Done = done,
                PromptEvalCount = promptTokens,
                EvalCount = completionTokens
            };
        }
        catch (Exception ex)
        {
            // Return a basic response with error info
            return new ChatResponse
            {
                Id = "error",
                Model = "error",
                CreatedAt = System.DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
                Message = new MessageResponse { Role = "assistant", Content = $"Error parsing response: {ex.Message}" },
                Done = true
            };
        }
    }

    private StreamChunk ParseStreamingChunk(string json)
    {
        try
        {
            // Parse streaming chunk (assuming OpenAI-compatible SSE format)
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string content = string.Empty;
            bool done = false;
            int? promptTokens = null;
            int? completionTokens = null;

            // Extract content from delta
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentProp))
                {
                    content = contentProp.GetString() ?? string.Empty;
                }

                // Check if done
                if (choices[0].TryGetProperty("finish_reason", out var finishReason))
                {
                    done = !string.IsNullOrEmpty(finishReason.GetString());
                }
            }

            // Extract usage if available
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt))
                {
                    promptTokens = prompt.GetInt32();
                }
                if (usage.TryGetProperty("completion_tokens", out var completion))
                {
                    completionTokens = completion.GetInt32();
                }
            }

            return new StreamChunk
            {
                Content = content,
                Done = done,
                PromptEvalCount = promptTokens,
                EvalCount = completionTokens
            };
        }
        catch
        {
            // Return empty chunk on parse error
            return new StreamChunk { Content = string.Empty, Done = false };
        }
    }

    private bool finishKindToBoolean(string? finishReason)
    {
        // null or empty means not finished yet
        return string.IsNullOrEmpty(finishReason);
    }

    public override Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        // For now, return empty list - in a full implementation we would
        // call the models endpoint and transform the results
        // For Phase 1, we'll rely on predefined models or user input
        return Task.FromResult(new List<ModelInfo>());
    }

    public override ServiceInfo GetServiceInfo()
    {
        return new ServiceInfo
        {
            Provider = "Generic HTTP",
            Version = "1.0",
            RequiresApiKey = !string.IsNullOrEmpty(_config.ApiKey),
            SupportsStreaming = _config.SupportsStreaming,
            Capabilities = new List<string> { "chat", "generate", _config.SupportsStreaming ? "streaming" : "" }
        };
    }

    public override async Task<bool> TestConnectionAsync()
    {
        // Try to connect to the models endpoint or make a minimal request
        try
        {
            // Try models endpoint first
            if (!string.IsNullOrEmpty(_config.Endpoints.Models))
            {
                var (_, error) = await GetRequestAsync(_config.Endpoints.Models);
                if (error == null)
                {
                    return true;
                }
            }

            // Fallback: try a minimal chat request
            var request = new { model = "test", messages = new[] { new { role = "user", content = "test" } }, max_tokens = 1 };
            var (_, error_) = await PostRequestAsync(_config.Endpoints.Chat, request);
            // Even if we get an error like "model not found", the connection worked
            return error_ == null || error_.Contains("model") || error_.Contains("not found") || error_.Contains("invalid");
        }
        catch
        {
            return false;
        }
    }
}