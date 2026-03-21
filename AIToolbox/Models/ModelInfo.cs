using System.Text.Json.Serialization;

namespace AIToolbox.Models;

/// <summary>
/// 模型信息类
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// 模型唯一标识符（用于 API 调用）
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 模型显示名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模型描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 模型提供商
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 模型版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 上下文长度（Token 数）
    /// </summary>
    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; } = 8192;

    /// <summary>
    /// 最大输出 Token 数
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 是否推荐使用
    /// </summary>
    [JsonPropertyName("recommended")]
    public bool Recommended { get; set; }

    /// <summary>
    /// 是否免费
    /// </summary>
    [JsonPropertyName("is_free")]
    public bool IsFree { get; set; }

    /// <summary>
    /// 模型大小（如 "7B", "70B"）
    /// </summary>
    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// 模型能力列表
    /// </summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// 定价信息（每百万 Token）
    /// </summary>
    [JsonPropertyName("pricing")]
    public PricingInfo? Pricing { get; set; }

    /// <summary>
    /// 模型元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 模型标签
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 模型图标 URL
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// 模型文档 URL
    /// </summary>
    [JsonPropertyName("documentation_url")]
    public string? DocumentationUrl { get; set; }
}

/// <summary>
/// 定价信息
/// </summary>
public class PricingInfo
{
    /// <summary>
    /// 输入价格（每百万 Token）
    /// </summary>
    [JsonPropertyName("input_price_per_million")]
    public decimal InputPricePerMillion { get; set; }

    /// <summary>
    /// 输出价格（每百万 Token）
    /// </summary>
    [JsonPropertyName("output_price_per_million")]
    public decimal OutputPricePerMillion { get; set; }

    /// <summary>
    /// 货币单位（如 USD, CNY）
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// 是否按量计费
    /// </summary>
    [JsonPropertyName("pay_as_you_go")]
    public bool PayAsYouGo { get; set; } = true;

    public string GetPriceDisplay()
    {
        if (InputPricePerMillion == 0 && OutputPricePerMillion == 0)
            return "免费";

        return $"¥{InputPricePerMillion:F2}/百万输入 | ¥{OutputPricePerMillion:F2}/百万输出";
    }
}

/// <summary>
/// 模型扩展方法
/// </summary>
public static class ModelInfoExtensions
{
    /// <summary>
    /// 获取模型显示文本
    /// </summary>
    public static string GetDisplayText(this ModelInfo model)
    {
        var parts = new List<string>();
        parts.Add(model.Name);

        if (!string.IsNullOrEmpty(model.Size))
            parts.Add($"({model.Size})");

        if (model.Recommended)
            parts.Add("⭐推荐");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 获取详细描述
    /// </summary>
    public static string GetDetailedDescription(this ModelInfo model)
    {
        var lines = new List<string>();
        lines.Add($"📋 {model.Name}");

        if (!string.IsNullOrEmpty(model.Description))
            lines.Add($"   {model.Description}");

        if (!string.IsNullOrEmpty(model.Provider))
            lines.Add($"   🏢 提供商: {model.Provider}");

        if (!string.IsNullOrEmpty(model.Size))
            lines.Add($"   📦 大小: {model.Size}");

        if (model.ContextLength > 0)
            lines.Add($"   📝 上下文: {model.ContextLength:N0} tokens");

        if (model.MaxTokens > 0)
            lines.Add($"   🔤 最大输出: {model.MaxTokens:N0} tokens");

        if (model.Capabilities.Any())
            lines.Add($"   ✨ 能力: {string.Join(", ", model.Capabilities)}");

        if (model.Languages.Any())
            lines.Add($"   🌐 语言: {string.Join(", ", model.Languages)}");

        if (model.Pricing != null)
            lines.Add($"   💰 定价: {model.Pricing.GetPriceDisplay()}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 检查是否支持特定能力
    /// </summary>
    public static bool HasCapability(this ModelInfo model, string capability)
    {
        return model.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查是否支持特定语言
    /// </summary>
    public static bool SupportsLanguage(this ModelInfo model, string language)
    {
        return model.Languages.Contains(language, StringComparer.OrdinalIgnoreCase);
    }
}