/// <summary>
/// 服务提供商信息
/// </summary>
public class ServiceInfo
{
    public string Provider { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool RequiresApiKey { get; set; }
    public bool SupportsStreaming { get; set; }
    public List<string> Capabilities { get; set; } = new();
}