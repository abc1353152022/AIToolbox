using AIToolbox.Models;

namespace AIToolbox.Services;

/// <summary>
/// 统一的 AI 服务接口
/// </summary>
public interface IAIService
{
    /// <summary>
    /// 发送聊天消息（非流式）
    /// </summary>
    Task<(ChatResponse? Response, string? Error)> SendMessageAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null);

    /// <summary>
    /// 发送聊天消息（流式）
    /// </summary>
    IAsyncEnumerable<StreamChunk> SendMessageStreamAsync(
        string model,
        List<Message> messages,
        Dictionary<string, object>? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    Task<List<ModelInfo>> GetAvailableModelsAsync();

    /// <summary>
    /// 获取当前服务提供商信息
    /// </summary>
    ServiceInfo GetServiceInfo();

    /// <summary>
    /// 测试连接
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// 更新配置
    /// </summary>
    void UpdateConfig(Dictionary<string, string> config);
}

/// <summary>
/// 流式响应块
/// </summary>
public class StreamChunk
{
    public string Content { get; set; } = string.Empty;
    public bool Done { get; set; }
    public int? PromptEvalCount { get; set; }
    public int? EvalCount { get; set; }
    public long? TotalDuration { get; set; }
}