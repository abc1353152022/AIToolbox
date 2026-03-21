namespace AIToolbox.Utils;

public static class ConsoleHelper
{
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
}