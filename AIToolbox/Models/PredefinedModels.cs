namespace AIToolbox.Models;

/// <summary>
/// 预定义的模型集合
/// </summary>
public static class PredefinedModels
{
    /// <summary>
    /// Ollama 模型
    /// </summary>
    public static class Ollama
    {
        public static ModelInfo Llama32 => new()
        {
            Id = "llama3.2",
            Name = "Llama 3.2",
            Description = "Meta 最新轻量级开源模型，适合快速响应",
            Provider = "Meta",
            Version = "3.2",
            ContextLength = 131072,
            MaxTokens = 4096,
            Recommended = true,
            IsFree = true,
            Size = "1B/3B",
            Languages = new() { "en", "zh", "es", "fr", "de" },
            Capabilities = new() { "chat", "text-generation" },
            Tags = new() { "fast", "lightweight", "multilingual" }
        };

        public static ModelInfo Qwen25 => new()
        {
            Id = "qwen2.5",
            Name = "Qwen 2.5",
            Description = "阿里千问系列，中文理解能力强",
            Provider = "Alibaba",
            Version = "2.5",
            ContextLength = 131072,
            MaxTokens = 8192,
            Recommended = true,
            IsFree = true,
            Size = "0.5B-72B",
            Languages = new() { "zh", "en" },
            Capabilities = new() { "chat", "text-generation", "coding" },
            Tags = new() { "chinese", "coding", "versatile" }
        };

        public static ModelInfo DeepSeekR1 => new()
        {
            Id = "deepseek-r1",
            Name = "DeepSeek R1",
            Description = "DeepSeek 推理增强模型，擅长逻辑推理",
            Provider = "DeepSeek",
            Version = "r1",
            ContextLength = 131072,
            MaxTokens = 8192,
            Recommended = true,
            IsFree = true,
            Size = "1.5B-70B",
            Languages = new() { "zh", "en" },
            Capabilities = new() { "chat", "reasoning", "coding" },
            Tags = new() { "reasoning", "math", "coding" }
        };

        public static ModelInfo Gemma2 => new()
        {
            Id = "gemma2",
            Name = "Gemma 2",
            Description = "Google 开源模型，安全可靠",
            Provider = "Google",
            Version = "2",
            ContextLength = 8192,
            MaxTokens = 4096,
            Recommended = true,
            IsFree = true,
            Size = "2B/9B/27B",
            Languages = new() { "en" },
            Capabilities = new() { "chat", "text-generation" },
            Tags = new() { "google", "safe", "research" }
        };
    }

    /// <summary>
    /// OpenAI 模型
    /// </summary>
    public static class OpenAI
    {
        public static ModelInfo Gpt4o => new()
        {
            Id = "gpt-4o",
            Name = "GPT-4o",
            Description = "OpenAI 最新多模态模型，支持文本、图像、音频",
            Provider = "OpenAI",
            Version = "gpt-4o",
            ContextLength = 128000,
            MaxTokens = 16384,
            Recommended = true,
            IsFree = false,
            Size = "超大",
            Languages = new() { "多语言" },
            Capabilities = new() { "chat", "vision", "audio", "text-generation" },
            Pricing = new PricingInfo
            {
                InputPricePerMillion = 2.50m,
                OutputPricePerMillion = 10.00m,
                Currency = "USD"
            },
            Tags = new() { "multimodal", "flagship", "powerful" }
        };

        public static ModelInfo Gpt4Turbo => new()
        {
            Id = "gpt-4-turbo",
            Name = "GPT-4 Turbo",
            Description = "GPT-4 增强版，更快的响应速度",
            Provider = "OpenAI",
            Version = "gpt-4-turbo",
            ContextLength = 128000,
            MaxTokens = 4096,
            Recommended = true,
            IsFree = false,
            Size = "超大",
            Languages = new() { "多语言" },
            Capabilities = new() { "chat", "text-generation" },
            Pricing = new PricingInfo
            {
                InputPricePerMillion = 10.00m,
                OutputPricePerMillion = 30.00m,
                Currency = "USD"
            },
            Tags = new() { "fast", "powerful" }
        };

        public static ModelInfo Gpt35Turbo => new()
        {
            Id = "gpt-3.5-turbo",
            Name = "GPT-3.5 Turbo",
            Description = "快速、经济的模型，适合日常任务",
            Provider = "OpenAI",
            Version = "gpt-3.5-turbo",
            ContextLength = 16385,
            MaxTokens = 4096,
            Recommended = false,
            IsFree = false,
            Size = "175B",
            Languages = new() { "多语言" },
            Capabilities = new() { "chat", "text-generation" },
            Pricing = new PricingInfo
            {
                InputPricePerMillion = 0.50m,
                OutputPricePerMillion = 1.50m,
                Currency = "USD"
            },
            Tags = new() { "fast", "cheap", "reliable" }
        };
    }

    /// <summary>
    /// 获取所有预定义模型
    /// </summary>
    public static List<ModelInfo> GetAll()
    {
        var models = new List<ModelInfo>();

        // Ollama 模型
        models.Add(Ollama.Llama32);
        models.Add(Ollama.Qwen25);
        models.Add(Ollama.DeepSeekR1);
        models.Add(Ollama.Gemma2);

        // OpenAI 模型
        models.Add(OpenAI.Gpt4o);
        models.Add(OpenAI.Gpt4Turbo);
        models.Add(OpenAI.Gpt35Turbo);

        return models;
    }

    /// <summary>
    /// 获取推荐模型
    /// </summary>
    public static List<ModelInfo> GetRecommended()
    {
        return GetAll().Where(m => m.Recommended).ToList();
    }

    /// <summary>
    /// 按提供商分组
    /// </summary>
    public static Dictionary<string, List<ModelInfo>> GroupByProvider()
    {
        return GetAll()
            .GroupBy(m => m.Provider)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 按能力筛选
    /// </summary>
    public static List<ModelInfo> FilterByCapability(string capability)
    {
        return GetAll()
            .Where(m => m.HasCapability(capability))
            .ToList();
    }
}