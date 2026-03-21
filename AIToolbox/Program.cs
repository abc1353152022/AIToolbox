using System.Text;
using System.Text.Json;
using AIToolbox.Models;
using AIToolbox.Services;
using AIToolbox.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AIToolbox;

class Program
{
    private static IAIService? _aiService;
    private static string? _currentProvider;
    private static string? _currentModel;
    private static List<Message> _conversationHistory = new();
    private static bool _useStreaming = true;
    private static AppSettings _appSettings = new();
    private static string? _lastError;

    // 服务实例
    private static OllamaService? _ollamaService;
    private static AitoolsService? _aitoolsService;
    private static DeepSeekService? _deepseekService;
    private static OpenAIService? _openaiService;

    // 统计信息
    private static int _totalTokens;
    private static int _messageCount;
    private static DateTime _sessionStartTime;

    static async Task Main(string[] args)
    {
        Console.Title = "AI工具箱";
        _sessionStartTime = DateTime.Now;

        try
        {
            ConsoleHelper.DisplayHeader("AI工具箱 - 多提供商 AI 对话助手");

            LoadConfiguration();

            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            var httpClient = serviceProvider.GetRequiredService<HttpClient>();

            InitializeServices(httpClient);
            await SelectProviderAsync();
            await TestConnectionAsync();
            await SelectModelAsync();
            ShowHelp();

            ConsoleHelper.WriteLineColored($"\n💨 流式输出: {(_useStreaming ? "✅ 已启用" : "❌ 已禁用")}",
                _useStreaming ? ConsoleColor.Green : ConsoleColor.Yellow);
            ConsoleHelper.WriteLineColored("   输入 /stream 可切换模式", ConsoleColor.DarkGray);

            await MainLoopAsync();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLineColored($"程序运行错误: {ex.Message}", ConsoleColor.Red);
            ConsoleHelper.WriteLineColored("按任意键退出...", ConsoleColor.Gray);
            Console.ReadKey();
        }
    }

    static void InitializeServices(HttpClient httpClient)
    {
        _ollamaService = AIServiceFactory.CreateOllama(httpClient);
        _aitoolsService = AIServiceFactory.CreateAitools(httpClient);
        _deepseekService = AIServiceFactory.CreateDeepSeek(httpClient);
        _openaiService = AIServiceFactory.CreateOpenAI(httpClient);
    }

    static void LoadConfiguration()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            ConsoleHelper.WriteLineColored("⚠️  未找到 appsettings.json，使用默认配置", ConsoleColor.Yellow);
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            _appSettings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? new AppSettings();

            ConsoleHelper.WriteLineColored($"✅ 已加载配置文件", ConsoleColor.Green);
            ConsoleHelper.WriteLineColored($"   默认提供商: {_appSettings.DefaultProvider}", ConsoleColor.DarkGray);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLineColored($"⚠️  配置文件加载失败: {ex.Message}，使用默认配置", ConsoleColor.Yellow);
        }
    }

    static IAIService? GetServiceForProvider(string provider) => provider.ToLower() switch
    {
        "ollama" => _ollamaService,
        "aitools" => _aitoolsService,
        "deepseek" => _deepseekService,
        "openai" => _openaiService,
        _ => null
    };

    static async Task SelectProviderAsync()
    {
        ConsoleHelper.WriteLineColored("选择 AI 服务提供商:", ConsoleColor.Cyan);
        Console.WriteLine();

        var providers = new List<(string id, string name, string url, bool hasConfig)>
        {
            ("ollama", "Ollama", "localhost:11434", _appSettings.Providers.ContainsKey("ollama")),
            ("aitools", "AITools", "api.aitools.cfd", _appSettings.Providers.ContainsKey("aitools")),
            ("deepseek", "DeepSeek", "api.deepseek.com", _appSettings.Providers.ContainsKey("deepseek")),
            ("openai", "OpenAI", "api.openai.com", _appSettings.Providers.ContainsKey("openai"))
        };

        for (int i = 0; i < providers.Count; i++)
        {
            var (id, name, url, hasConfig) = providers[i];
            var configMark = hasConfig ? " ✅" : "";
            ConsoleHelper.WriteLineColored($"  {i + 1}. {name} ({url}){configMark}", ConsoleColor.White);
        }
        Console.WriteLine();

        var defaultIndex = providers.FindIndex(p => p.id == _appSettings.DefaultProvider?.ToLower());
        if (defaultIndex >= 0)
        {
            ConsoleHelper.WriteLineColored($"默认: {providers[defaultIndex].name}", ConsoleColor.DarkGray);
        }

        var input = ConsoleHelper.ReadInput("请输入选项 (直接回车使用默认): ", ConsoleColor.Green);

        string providerId;
        if (string.IsNullOrWhiteSpace(input))
        {
            providerId = _appSettings.DefaultProvider?.ToLower() ?? "ollama";
        }
        else
        {
            var index = int.TryParse(input, out var i) ? i - 1 : -1;
            providerId = (index >= 0 && index < providers.Count) ? providers[index].id : _appSettings.DefaultProvider?.ToLower() ?? "ollama";
        }

        _currentProvider = providerId;
        _aiService = GetServiceForProvider(providerId);
        ConfigureProvider(providerId);

        ConsoleHelper.WriteLineColored($"\n✅ 已选择提供商: {providerId}", ConsoleColor.Green);
        Console.WriteLine();
    }

    static void ConfigureProvider(string providerId)
    {
        if (_appSettings.Providers.TryGetValue(providerId, out var config) && !string.IsNullOrEmpty(config.ApiKey))
        {
            _aiService?.UpdateConfig(new Dictionary<string, string>
            {
                ["apiKey"] = config.ApiKey,
                ["baseUrl"] = config.BaseUrl ?? GetDefaultBaseUrl(providerId)
            });
            ConsoleHelper.WriteLineColored("   已加载配置文件中的 API Key", ConsoleColor.DarkGray);
        }
    }

    static string GetDefaultBaseUrl(string providerId) => providerId.ToLower() switch
    {
        "ollama" => "https://api.ollama.com",
        "aitools" => "https://platform.aitools.cfd",
        "deepseek" => "https://api.deepseek.com",
        "openai" => "https://api.openai.com",
        _ => ""
    };

    static async Task TestConnectionAsync()
    {
        ConsoleHelper.WriteColored("测试连接", ConsoleColor.Yellow);
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(300);
            ConsoleHelper.WriteColored(".", ConsoleColor.Yellow);
        }
        Console.WriteLine();

        var isConnected = await _aiService!.TestConnectionAsync();

        if (isConnected)
        {
            var info = _aiService.GetServiceInfo();
            ConsoleHelper.WriteLineColored($"✅ 连接成功！", ConsoleColor.Green);
            ConsoleHelper.WriteLineColored($"   提供商: {info.Provider}", ConsoleColor.DarkGray);
            ConsoleHelper.WriteLineColored($"   需要 API Key: {(info.RequiresApiKey ? "是" : "否")}", ConsoleColor.DarkGray);
            ConsoleHelper.WriteLineColored($"   支持流式: {(info.SupportsStreaming ? "是" : "否")}", ConsoleColor.DarkGray);
        }
        else
        {
            var info = _aiService.GetServiceInfo();
            ConsoleHelper.WriteLineColored($"⚠️  无法连接到 {info.Provider} 服务", ConsoleColor.Yellow);
            ConsoleHelper.WriteLineColored($"   错误: {_lastError ?? "未知错误"}", ConsoleColor.DarkGray);

            var continueAnyway = ConsoleHelper.ReadInput("\n是否继续? (y/n): ", ConsoleColor.Red);
            if (continueAnyway?.ToLower() != "y")
            {
                Environment.Exit(0);
            }
        }

        Console.WriteLine();
    }

    static async Task SelectModelAsync()
    {
        var models = await _aiService!.GetAvailableModelsAsync();

        ConsoleHelper.WriteLineColored("可用模型:", ConsoleColor.Cyan);

        var recommended = models.Where(m => m.Recommended).ToList();
        if (recommended.Any())
        {
            ConsoleHelper.WriteLineColored("\n⭐ 推荐模型:", ConsoleColor.Yellow);
            foreach (var model in recommended)
            {
                ConsoleHelper.WriteColored($"  • {model.Id}", ConsoleColor.Green);
                ConsoleHelper.WriteColored($" - {model.Name}", ConsoleColor.White);
                if (!string.IsNullOrEmpty(model.Description))
                {
                    ConsoleHelper.WriteColored($" ({model.Description})", ConsoleColor.DarkGray);
                }
                Console.WriteLine();
            }
        }

        var others = models.Where(m => !m.Recommended).ToList();
        if (others.Any())
        {
            ConsoleHelper.WriteLineColored("\n📦 其他模型:", ConsoleColor.DarkGray);
            foreach (var model in others)
            {
                ConsoleHelper.WriteColored($"  • {model.Id}", ConsoleColor.Gray);
                if (!string.IsNullOrEmpty(model.Description))
                {
                    ConsoleHelper.WriteColored($" - {model.Description}", ConsoleColor.DarkGray);
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine();

        while (true)
        {
            var input = ConsoleHelper.ReadInput("请输入模型名称: ", ConsoleColor.Green);
            if (!string.IsNullOrWhiteSpace(input))
            {
                _currentModel = input.Trim();
                break;
            }
            ConsoleHelper.WriteLineColored("模型名称不能为空", ConsoleColor.Red);
        }

        ConsoleHelper.WriteLineColored($"\n✅ 已选择模型: {_currentModel}", ConsoleColor.Green);
        Console.WriteLine();
    }

    static void ShowHelp()
    {
        ConsoleHelper.WriteLineColored("使用说明:", ConsoleColor.Cyan);
        ConsoleHelper.WriteLineColored("  /new      - 开始新对话", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /model    - 切换模型", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /clear    - 清空对话历史", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /provider - 切换提供商", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /stream   - 切换流式输出模式", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /retry    - 重试上次请求", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /info     - 显示服务信息", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /stats    - 显示统计信息", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /history  - 显示对话历史", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /copy     - 复制上次回复", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /export   - 导出对话历史", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /help     - 显示帮助", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /quit     - 退出程序", ConsoleColor.White);
        Console.WriteLine();
    }

    static string? _lastAssistantMessage;

    static async Task MainLoopAsync()
    {
        while (true)
        {
            ConsoleHelper.WriteColored("\n您: ", ConsoleColor.Green);
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.StartsWith("/"))
            {
                await HandleCommandAsync(input);
                continue;
            }

            await SendMessageAsync(input);
        }
    }

    static async Task SendMessageAsync(string input, bool isRetry = false)
    {
        if (!isRetry)
        {
            _conversationHistory.Add(new Message { Role = "user", Content = input });
        }

        try
        {
            if (_useStreaming && _aiService!.GetServiceInfo().SupportsStreaming)
            {
                await HandleStreamResponseAsync();
            }
            else
            {
                await HandleNormalResponseAsync();
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLineColored($"\n❌ 系统错误: {ex.Message}", ConsoleColor.Red);
            if (!isRetry)
            {
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
            }
        }
    }

    static async Task HandleStreamResponseAsync()
    {
        ConsoleHelper.WriteColored("\n🤖 AI: ", ConsoleColor.Cyan);

        var fullContent = new StringBuilder();
        var hasContent = false;
        var dotCount = 0;

        await foreach (var chunk in _aiService!.SendMessageStreamAsync(_currentModel!, _conversationHistory))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                Console.Write(chunk.Content);
                fullContent.Append(chunk.Content);
                hasContent = true;
                dotCount = 0;
            }
            else if (!hasContent && !chunk.Done)
            {
                dotCount++;
                if (dotCount % 10 == 1)
                    Console.Write(".");
            }

            if (chunk.Done)
            {
                Console.WriteLine();
                _lastAssistantMessage = fullContent.ToString();
                _conversationHistory.Add(new Message { Role = "assistant", Content = _lastAssistantMessage });

                if (chunk.PromptEvalCount.HasValue && chunk.EvalCount.HasValue)
                {
                    _totalTokens += chunk.PromptEvalCount.Value + chunk.EvalCount.Value;
                    _messageCount++;
                    ConsoleHelper.WriteColored($"\n📊 [输入: {chunk.PromptEvalCount} | 输出: {chunk.EvalCount} | 总计: {_totalTokens}]", ConsoleColor.DarkGray);
                }
                Console.WriteLine();
                break;
            }
        }
    }

    static async Task HandleNormalResponseAsync()
    {
        if (!_useStreaming)
            ConsoleHelper.WriteColored("AI 正在思考 (非流式模式)", ConsoleColor.Yellow);
        else
            ConsoleHelper.WriteColored("AI 正在思考", ConsoleColor.Yellow);

        var spinner = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        var spinnerTask = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                Console.Write($"\b{spinner[i % spinner.Length]}");
                await Task.Delay(100);
            }
        });

        var (response, error) = await _aiService!.SendMessageAsync(_currentModel!, _conversationHistory);
        Console.Write("\b \b");

        if (response != null && response.Message != null)
        {
            var assistantMessage = response.Message.Content;
            _lastAssistantMessage = assistantMessage;

            ConsoleHelper.WriteColored("\n🤖 AI: ", ConsoleColor.Cyan);
            Console.WriteLine(assistantMessage);

            _conversationHistory.Add(new Message { Role = "assistant", Content = assistantMessage });

            _totalTokens += response.PromptEvalCount + response.EvalCount;
            _messageCount++;

            ConsoleHelper.WriteColored($"\n📊 [输入: {response.PromptEvalCount} | 输出: {response.EvalCount} | 总计: {_totalTokens}]", ConsoleColor.DarkGray);
            if (response.TotalDuration > 0)
            {
                var durationMs = response.TotalDuration / 1_000_000.0;
                ConsoleHelper.WriteColored($" | 耗时: {durationMs:F0}ms", ConsoleColor.DarkGray);
            }
            Console.WriteLine();
        }
        else
        {
            ConsoleHelper.WriteLineColored($"\n❌ 错误: {error}", ConsoleColor.Red);
            _lastError = error;
            _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
        }
    }

    static async Task HandleCommandAsync(string command)
    {
        switch (command.ToLower())
        {
            case "/new":
                _conversationHistory.Clear();
                _lastAssistantMessage = null;
                ConsoleHelper.WriteLineColored("✨ 已开始新对话！", ConsoleColor.Green);
                break;

            case "/model":
                await SelectModelAsync();
                break;

            case "/clear":
                _conversationHistory.Clear();
                _lastAssistantMessage = null;
                ConsoleHelper.WriteLineColored("🗑️ 对话历史已清空！", ConsoleColor.Green);
                break;

            case "/provider":
                await SelectProviderAsync();
                _conversationHistory.Clear();
                _lastAssistantMessage = null;
                await TestConnectionAsync();
                await SelectModelAsync();
                break;

            case "/stream":
                _useStreaming = !_useStreaming;
                ConsoleHelper.WriteLineColored($"\n💨 流式输出已{(_useStreaming ? "启用" : "禁用")}",
                    _useStreaming ? ConsoleColor.Green : ConsoleColor.Yellow);
                break;

            case "/retry":
                if (_conversationHistory.Count > 0 && _conversationHistory.Last().Role == "user")
                {
                    _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
                    await SendMessageAsync("", isRetry: true);
                }
                else if (_conversationHistory.Count > 0 && _conversationHistory.Last().Role == "assistant")
                {
                    _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
                    await SendMessageAsync("", isRetry: true);
                }
                else
                {
                    ConsoleHelper.WriteLineColored("没有可以重试的请求", ConsoleColor.Yellow);
                }
                break;

            case "/info":
                var info = _aiService!.GetServiceInfo();
                ConsoleHelper.WriteLineColored("\n📡 服务信息:", ConsoleColor.Cyan);
                ConsoleHelper.WriteLineColored($"   提供商: {info.Provider}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"   版本: {info.Version}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"   需要 API Key: {(info.RequiresApiKey ? "是" : "否")}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"   支持流式: {(info.SupportsStreaming ? "是" : "否")}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"   能力: {string.Join(", ", info.Capabilities)}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"   当前模型: {_currentModel}", ConsoleColor.White);
                break;

            case "/stats":
                var elapsed = DateTime.Now - _sessionStartTime;
                ConsoleHelper.WriteLineColored($"\n📈 统计信息:", ConsoleColor.Cyan);
                ConsoleHelper.WriteLineColored($"  💬 对话轮次: {_messageCount}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"  🤖 当前模型: {_currentModel}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"  📝 历史消息数: {_conversationHistory.Count}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"  🔧 提供商: {_currentProvider}", ConsoleColor.White);
                ConsoleHelper.WriteLineColored($"  💨 流式输出: {(_useStreaming ? "启用" : "禁用")}", _useStreaming ? ConsoleColor.Green : ConsoleColor.Yellow);
                ConsoleHelper.WriteLineColored($"  ⏱️  会话时长: {elapsed:hh\\:mm\\:ss}", ConsoleColor.White);
                break;

            case "/history":
                if (_conversationHistory.Count == 0)
                {
                    ConsoleHelper.WriteLineColored("对话历史为空", ConsoleColor.Yellow);
                }
                else
                {
                    ConsoleHelper.WriteLineColored("\n📜 对话历史:", ConsoleColor.Cyan);
                    for (int i = 0; i < _conversationHistory.Count; i++)
                    {
                        var msg = _conversationHistory[i];
                        var role = msg.Role == "user" ? "👤 您" : "🤖 AI";
                        var preview = msg.Content.Length > 100 ? msg.Content.Substring(0, 100) + "..." : msg.Content;
                        ConsoleHelper.WriteColored($"  [{i + 1}] {role}: {preview}", msg.Role == "user" ? ConsoleColor.Green : ConsoleColor.Cyan);
                    }
                }
                break;

            case "/copy":
                if (!string.IsNullOrEmpty(_lastAssistantMessage))
                {
                    try
                    {
                        var tempFile = Path.GetTempFileName();
                        File.WriteAllText(tempFile, _lastAssistantMessage);
                        File.Copy(tempFile, "last_response.txt", true);
                        ConsoleHelper.WriteLineColored("✅ 已保存到 last_response.txt", ConsoleColor.Green);
                    }
                    catch
                    {
                        ConsoleHelper.WriteLineColored("复制失败", ConsoleColor.Red);
                    }
                }
                else
                {
                    ConsoleHelper.WriteLineColored("没有可复制的内容", ConsoleColor.Yellow);
                }
                break;

            case "/export":
                ExportConversation();
                break;

            case "/help":
                ShowHelp();
                break;

            case "/quit":
            case "/exit":
                ConsoleHelper.WriteLineColored("👋 感谢使用，再见！", ConsoleColor.Cyan);
                Environment.Exit(0);
                break;

            default:
                ConsoleHelper.WriteLineColored("❓ 未知命令，输入 /help 查看帮助。", ConsoleColor.Red);
                break;
        }
    }

    static void ExportConversation()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"conversation_{timestamp}.json";

        var exportData = new
        {
            provider = _currentProvider,
            model = _currentModel,
            exportedAt = DateTime.Now,
            messages = _conversationHistory
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filename, json);

        ConsoleHelper.WriteLineColored($"✅ 对话已导出到: {filename}", ConsoleColor.Green);
    }
}
