namespace AIToolbox.Models;

public class AppSettings
{
    public string DefaultProvider { get; set; } = "ollama";
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}

public class ProviderConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
