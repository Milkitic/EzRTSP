using System.Collections.Concurrent;

namespace EzRTSP.Common.Utils;

public static class ConsoleHelper
{
    private static readonly ConcurrentQueue<ConsoleDescription[]> ConsoleQueue = new();
    static ConsoleHelper()
    {
        var cts = new CancellationTokenSource();
        var task = Task.Factory.StartNew(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                PrintNext();
                Thread.Sleep(3);
            }
        }, TaskCreationOptions.LongRunning);

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            cts.Cancel();
            task.Wait();
        };
    }

    private static void PrintNext()
    {
        while (!ConsoleQueue.IsEmpty)
        {
            var result = ConsoleQueue.TryDequeue(out var item);
            if (!result) continue;

            foreach (var consoleDescription in item!)
            {
                Console.ResetColor();
                if (consoleDescription.ForeColor != null)
                    Console.ForegroundColor = consoleDescription.ForeColor.Value;
                if (consoleDescription.BackColor != null)
                    Console.BackgroundColor = consoleDescription.BackColor.Value;
                Console.Write(consoleDescription.Content);
            }

            Console.WriteLine();
            Console.ResetColor();
        }
    }

    public static void WriteInfo(string data, string module)
    {
        WriteLine(new ConsoleDescription($"[{module}]", ConsoleColor.DarkCyan),
            new ConsoleDescription($" [info] {data}"));
    }

    public static void WriteWarn(string data, string module)
    {
        WriteLine(new ConsoleDescription($"[{module}]", ConsoleColor.DarkCyan),
            new ConsoleDescription($" [warn] {data}", ConsoleColor.Yellow));
    }

    public static void WriteError(string data, string module)
    {
        WriteLine(new ConsoleDescription($"[{module}]", ConsoleColor.Magenta),
            new ConsoleDescription($" [error] {data}", ConsoleColor.Red));
    }

    public static void WriteLine(string content)
    {
        ConsoleQueue.Enqueue(new[] { new ConsoleDescription(content) });
    }

    public static void WriteLine(params ConsoleDescription[] contents)
    {
        ConsoleQueue.Enqueue(contents);
    }
}