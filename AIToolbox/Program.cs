using System.Text;
using System.Text.Json;
using AIToolbox.Models;
using AIToolbox.Services;
using AIToolbox.Utils;
using Microsoft.Extensions.DependencyInjection;

#if WINDOWSEXCEPTIONS
using System.Windows.Forms;
#endif

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
    private static GenericHttpService? _genericHttpService;

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
        _genericHttpService = AIServiceFactory.CreateGenericHttp(httpClient, new GenericProviderConfig());
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
        "generic-http" => _genericHttpService,
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
            ("openai", "OpenAI", "api.openai.com", _appSettings.Providers.ContainsKey("openai")),
            ("generic-http", "自定义 HTTP", "可配置", _appSettings.Providers.ContainsKey("generic-http"))
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
        if (_appSettings.Providers.TryGetValue(providerId, out var config))
        {
            if (providerId == "generic-http" && config.GenericConfig != null)
            {
                // Configure generic HTTP provider with its full configuration
                _aiService?.UpdateConfig(new Dictionary<string, string>
                {
                    ["apiKey"] = config.GenericConfig.ApiKey ?? string.Empty,
                    ["baseUrl"] = config.GenericConfig.BaseUrl,
                    ["supportsStreaming"] = config.GenericConfig.SupportsStreaming.ToString()
                });

                // Additional configuration could be passed here if needed
                ConsoleHelper.WriteLineColored("   已加载自定义 HTTP 提供商配置", ConsoleColor.DarkGray);
            }
            else if (!string.IsNullOrEmpty(config.ApiKey))
            {
                // Standard provider configuration
                _aiService?.UpdateConfig(new Dictionary<string, string>
                {
                    ["apiKey"] = config.ApiKey,
                    ["baseUrl"] = config.BaseUrl ?? GetDefaultBaseUrl(providerId)
                });
                ConsoleHelper.WriteLineColored("   已加载配置文件中的 API Key", ConsoleColor.DarkGray);
            }
        }
    }

    static string GetDefaultBaseUrl(string providerId) => providerId.ToLower() switch
    {
        "ollama" => "https://api.ollama.com",
        "aitools" => "https://platform.aitools.cfd",
        "deepseek" => "https://api.deepseek.com",
        "openai" => "https://api.openai.com",
        "generic-http" => "可配置",
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
        ConsoleHelper.WriteLineColored("  /export   - 导出对话历史 (JSON/MD/HTML)", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /import   - 导入对话历史", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /help     - 显示帮助", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  /quit     - 退出程序", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  [Shift+Enter] - 多行输入", ConsoleColor.DarkGray);
        ConsoleHelper.WriteLineColored("  [Tab]       - 命令补全", ConsoleColor.DarkGray);
        ConsoleHelper.WriteLineColored("  [↑/↓]       - 历史消息导航", ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    static string? _lastAssistantMessage;
    private static List<string> _inputHistory = new();

    static async Task MainLoopAsync()
    {
        while (true)
        {
            var input = ConsoleHelper.ReadInputAdvanced("\n您: ", ConsoleColor.Green, _inputHistory)?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            // 添加到输入历史（非命令）
            if (!input.StartsWith("/"))
            {
                _inputHistory.Add(input);
                // 限制历史记录数量
                if (_inputHistory.Count > 50)
                    _inputHistory.RemoveAt(0);
            }

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
                CopyLastResponse();
                break;

            case "/export":
                ExportConversation();
                break;

            case "/import":
                ImportConversation();
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

    static void CopyLastResponse()
    {
        if (string.IsNullOrEmpty(_lastAssistantMessage))
        {
            ConsoleHelper.WriteLineColored("没有可复制的内容", ConsoleColor.Yellow);
            return;
        }

        try
        {
            // 尝试使用剪贴板复制
            if (ConsoleHelper.CopyToClipboard(_lastAssistantMessage))
            {
                ConsoleHelper.WriteLineColored("✅ 已复制到剪贴板！", ConsoleColor.Green);
            }
            else
            {
                // 剪贴板复制失败，回退到文件保存
                var tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, _lastAssistantMessage);
                File.Copy(tempFile, "last_response.txt", true);
                ConsoleHelper.WriteLineColored("⚠️  剪贴板复制失败，已保存到 last_response.txt", ConsoleColor.Yellow);
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLineColored($"❌ 复制失败: {ex.Message}", ConsoleColor.Red);
        }
    }

    static void ExportConversation()
    {
        ConsoleHelper.WriteLineColored("选择导出格式:", ConsoleColor.Cyan);
        ConsoleHelper.WriteLineColored("  1. JSON  - 原始数据格式", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  2. Markdown - 可读性好，适合分享", ConsoleColor.White);
        ConsoleHelper.WriteLineColored("  3. HTML  - 格式化网页", ConsoleColor.White);
        Console.WriteLine();

        var input = ConsoleHelper.ReadInput("请输入选项 (1-3): ", ConsoleColor.Green);
        var format = input.Trim() switch
        {
            "2" => "md",
            "3" => "html",
            _ => "json"
        };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename;

        switch (format)
        {
            case "md":
                filename = $"conversation_{timestamp}.md";
                var markdownContent = ConsoleHelper.ExportToMarkdown(_conversationHistory, _currentProvider!, _currentModel!);
                File.WriteAllText(filename, markdownContent, Encoding.UTF8);
                break;
            case "html":
                filename = $"conversation_{timestamp}.html";
                var htmlContent = ConsoleHelper.ExportToHtml(_conversationHistory, _currentProvider!, _currentModel!);
                File.WriteAllText(filename, htmlContent, Encoding.UTF8);
                break;
            default:
                filename = $"conversation_{timestamp}.json";
                var exportData = new
                {
                    provider = _currentProvider,
                    model = _currentModel,
                    exportedAt = DateTime.Now,
                    messages = _conversationHistory
                };
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filename, json);
                break;
        }

        ConsoleHelper.WriteLineColored($"✅ 对话已导出到: {filename}", ConsoleColor.Green);
    }

    static void ImportConversation()
    {
        ConsoleHelper.WriteLineColored("拖入要导入的对话文件，或输入文件路径:", ConsoleColor.Cyan);
        var filepath = ConsoleHelper.ReadInput("文件路径: ", ConsoleColor.Green).Trim();

        // 移除引号（如果用户拖入文件）
        filepath = filepath.Trim('"', '\'');

        if (!File.Exists(filepath))
        {
            ConsoleHelper.WriteLineColored($"❌ 文件不存在: {filepath}", ConsoleColor.Red);
            return;
        }

        try
        {
            var content = File.ReadAllText(filepath);
            var ext = Path.GetExtension(filepath).ToLower();

            List<AIToolbox.Models.Message> importedMessages;

            if (ext == ".json")
            {
                var data = JsonSerializer.Deserialize<ImportData>(content);
                if (data?.messages != null)
                {
                    importedMessages = data.messages;
                }
                else
                {
                    ConsoleHelper.WriteLineColored("❌ 无效的 JSON 格式", ConsoleColor.Red);
                    return;
                }
            }
            else if (ext == ".md" || ext == ".html")
            {
                // 简单解析 Markdown/HTML 文件
                importedMessages = ParseMarkdownHtmlConversation(content, ext);
                if (importedMessages == null || importedMessages.Count == 0)
                {
                    ConsoleHelper.WriteLineColored("❌ 无法解析对话内容", ConsoleColor.Red);
                    return;
                }
            }
            else
            {
                ConsoleHelper.WriteLineColored("❌ 不支持的文件格式 (仅支持 .json, .md, .html)", ConsoleColor.Red);
                return;
            }

            // 确认导入
            ConsoleHelper.WriteLineColored($"\n(found {importedMessages.Count} 条消息)", ConsoleColor.DarkGray);
            var confirm = ConsoleHelper.ReadInput("确定要导入并覆盖当前对话吗? (y/n): ", ConsoleColor.Yellow);

            if (confirm.ToLower() == "y")
            {
                _conversationHistory = importedMessages;
                _lastAssistantMessage = importedMessages.LastOrDefault(m => m.Role == "assistant")?.Content;
                ConsoleHelper.WriteLineColored("✅ 对话导入成功！", ConsoleColor.Green);
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLineColored($"❌ 导入失败: {ex.Message}", ConsoleColor.Red);
        }
    }

    static List<Message> ParseMarkdownHtmlConversation(string content, string format)
    {
        var messages = new List<Message>();

        if (format == ".md")
        {
            // 解析 Markdown 格式
            var lines = content.Split('\n');
            var currentRole = "";
            var currentContent = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("## 👤 您") || line.StartsWith("## 🤖 AI"))
                {
                    if (!string.IsNullOrEmpty(currentRole))
                    {
                        messages.Add(new Message { Role = currentRole, Content = currentContent.ToString().Trim() });
                        currentContent.Clear();
                    }
                    currentRole = line.Contains("👤 您") ? "user" : "assistant";
                }
                else if (!string.IsNullOrEmpty(currentRole) && !line.StartsWith("#") && !line.StartsWith("---"))
                {
                    currentContent.AppendLine(line);
                }
            }

            if (!string.IsNullOrEmpty(currentRole) && currentContent.Length > 0)
            {
                messages.Add(new Message { Role = currentRole, Content = currentContent.ToString().Trim() });
            }
        }
        else if (format == ".html")
        {
            // 简单解析 HTML 格式
            var messageDivs = content.Split(new[] { "<div class=\"message" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var div in messageDivs)
            {
                var isUser = div.Contains("class=\"message user\"");
                var role = isUser ? "user" : "assistant";

                var contentStart = div.IndexOf("<div class=\"content\">");
                if (contentStart > 0)
                {
                    contentStart += "<div class=\"content\">".Length;
                    var contentEnd = div.IndexOf("</div>", contentStart);
                    if (contentEnd > contentStart)
                    {
                        var msgContent = div.Substring(contentStart, contentEnd - contentStart);
                        messages.Add(new Message { Role = role, Content = msgContent });
                    }
                }
            }
        }

        return messages;
    }

    class ImportData
    {
        public string? provider { get; set; }
        public string? model { get; set; }
        public DateTime exportedAt { get; set; }
        public List<Message>? messages { get; set; }
    }
}
