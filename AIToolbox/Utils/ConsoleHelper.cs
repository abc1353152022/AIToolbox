using System.Text;
using AIToolbox.Models;

#if WINDOWSEXCEPTIONS
using System.Windows.Forms;
#endif

namespace AIToolbox.Utils;

public static class ConsoleHelper
{
    private static readonly string[] _commandSuggestions = { "/new", "/model", "/clear", "/provider", "/stream", "/retry", "/info", "/stats", "/history", "/copy", "/export", "/help", "/quit" };

    public static void WriteColored(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteLineColored(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void DisplayHeader(string title)
    {
        Console.Clear();
        WriteLineColored(new string('=', 60), ConsoleColor.Cyan);
        WriteLineColored($"  {title}", ConsoleColor.Cyan);
        WriteLineColored(new string('=', 60), ConsoleColor.Cyan);
        Console.WriteLine();
    }

    public static void DisplayModelList(List<string> models)
    {
        WriteLineColored("支持的模型列表:", ConsoleColor.Yellow);
        for (int i = 0; i < models.Count; i++)
        {
            WriteColored($"{i + 1,2}. ", ConsoleColor.Gray);
            WriteLineColored(models[i], ConsoleColor.White);
        }
        Console.WriteLine();
    }

    public static string ReadInput(string prompt, ConsoleColor promptColor = ConsoleColor.Green)
    {
        WriteColored(prompt, promptColor);
        return Console.ReadLine() ?? string.Empty;
    }

    /// <summary>
    /// 读取一行输入，支持 Tab 命令补全和上下键历史导航
    /// Shift+Enter 添加换行，Enter 发送
    /// </summary>
    public static string ReadInputAdvanced(string prompt, ConsoleColor promptColor = ConsoleColor.Green, List<string>? history = null)
    {
        WriteColored(prompt, promptColor);

        var input = new StringBuilder();
        var historyIndex = -1;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);


            // Tab 键 - 命令补全
            if (key.Key == ConsoleKey.Tab)
            {
                if (input.Length > 0 && input.ToString().StartsWith("/"))
                {
                    var currentText = input.ToString();
                    var matches = _commandSuggestions.Where(c => c.StartsWith(currentText, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (matches.Count == 1)
                    {
                        input.Clear();
                        input.Append(matches[0]);
                        Console.Write(new string('\b', currentText.Length));
                        Console.Write(matches[0]);
                        continue;
                    }
                    else if (matches.Count > 1)
                    {
                        Console.WriteLine();
                        ConsoleHelper.WriteLineColored("可用补全:", ConsoleColor.DarkGray);
                        foreach (var m in matches)
                        {
                            ConsoleHelper.WriteLineColored($"  {m}", ConsoleColor.White);
                        }
                        WriteColored(prompt, promptColor);
                        Console.Write(input.ToString());
                    }
                }
                continue;
            }

            // 上箭头 - 历史导航
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (history != null && history.Count > 0)
                {
                    historyIndex = historyIndex < 0 ? history.Count - 1 : Math.Max(0, historyIndex - 1);
                    var historyText = history[historyIndex];
                    ClearCurrentLine();
                    WriteColored(prompt, promptColor);
                    input.Clear().Append(historyText);
                    Console.Write(historyText);
                }
                continue;
            }

            // 下箭头 - 历史导航
            if (key.Key == ConsoleKey.DownArrow)
            {
                if (history != null && history.Count > 0)
                {
                    historyIndex = historyIndex < 0 ? -1 : Math.Min(history.Count - 1, historyIndex + 1);
                    if (historyIndex >= 0)
                    {
                        var historyText = history[historyIndex];
                        ClearCurrentLine();
                        WriteColored(prompt, promptColor);
                        input.Clear().Append(historyText);
                        Console.Write(historyText);
                    }
                    else
                    {
                        ClearCurrentLine();
                        WriteColored(prompt, promptColor);
                        input.Clear();
                    }
                }
                continue;
            }

            // 回车 - 发送消息
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return input.ToString();
            }

            // 退格键
            if (key.Key == ConsoleKey.Backspace && input.Length > 0)
            {
                input.Length--;
                if (Console.CursorLeft == 0)
                {
                    Console.SetCursorPosition(Console.BufferWidth - 1, Console.CursorTop - 1);
                    Console.Write(' ');
                    Console.SetCursorPosition(Console.BufferWidth - 1, Console.CursorTop - 1);
                }
                else
                {
                    Console.Write("\b \b");
                }
                continue;
            }

            // 删除键
            if (key.Key == ConsoleKey.Delete)
            {
                continue;
            }

            // 其他字符
            if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }

    /// <summary>
    /// 清除当前行
    /// </summary>
    private static void ClearCurrentLine()
    {
        var cursorLeft = Console.CursorLeft;
        var bufferWidth = Console.BufferWidth;
        var spaces = new string(' ', bufferWidth);
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        Console.Write(spaces);
        Console.SetCursorPosition(0, Console.CursorTop - 1);
    }

    /// <summary>
    /// 导出对话为 Markdown 格式
    /// </summary>
    public static string ExportToMarkdown(List<Message> messages, string provider, string model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI 对话记录");
        sb.AppendLine();
        sb.AppendLine($"- **提供商**: {provider}");
        sb.AppendLine($"- **模型**: {model}");
        sb.AppendLine($"- **导出时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var roleTitle = msg.Role == "user" ? "👤 您" : "🤖 AI";
            sb.AppendLine($"## {roleTitle}");
            sb.AppendLine();
            sb.AppendLine(msg.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 导出对话为 HTML 格式
    /// </summary>
    public static string ExportToHtml(List<Message> messages, string provider, string model)
    {
        var html = $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>AI 对话记录</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 800px; margin: 40px auto; padding: 20px; line-height: 1.6; }}
        .header {{ border-bottom: 2px solid #e1e4e8; padding-bottom: 20px; margin-bottom: 30px; }}
        .header h1 {{ color: #24292e; margin: 0; }}
        .meta {{ color: #57606a; font-size: 14px; margin-top: 10px; }}
        .message {{ margin-bottom: 30px; padding: 20px; border-radius: 6px; }}
        .user {{ background-color: #f6f8fa; border: 1px solid #e1e4e8; }}
        .assistant {{ background-color: #f1f8ff; border: 1px solid #c8e6fb; }}
        .role {{ font-weight: bold; margin-bottom: 10px; font-size: 14px; }}
        .user .role {{ color: #22863a; }}
        .assistant .role {{ color: #0366d6; }}
        .content {{ color: #24292e; white-space: pre-wrap; }}
        .separator {{ border: none; border-top: 1px solid #e1e4e8; margin: 40px 0; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>AI 对话记录</h1>
        <div class=""meta"">
            <div>提供商: {provider}</div>
            <div>模型: {model}</div>
            <div>导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
        </div>
    </div>";

        foreach (var msg in messages)
        {
            var role = msg.Role == "user" ? "👤 您" : "🤖 AI";
            var className = msg.Role == "user" ? "user" : "assistant";
            var escapedContent = msg.Content.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

            html += $@"
    <div class=""message {className}"">
        <div class=""role"">{role}</div>
        <div class=""content"">{escapedContent}</div>
    </div>
    <div class=""separator""></div>";
        }

        html += @"
</body>
</html>";

        return html;
    }

#if WINDOWSEXCEPTIONS
    /// <summary>
    /// 复制文本到剪贴板（仅 Windows）
    /// </summary>
    public static bool CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
#else
    /// <summary>
    /// 复制文本到剪贴板（仅 Windows）
    /// </summary>
    public static bool CopyToClipboard(string text)
    {
        // 非 Windows 平台不支持剪贴板
        return false;
    }
#endif
}
