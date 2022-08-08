namespace EzRTSP.Common.Utils;

public class ConsoleDescription
{
    public ConsoleDescription(string content,
        ConsoleColor? foreColor = null,
        ConsoleColor? backColor = null)
    {
        Content = content;
        ForeColor = foreColor;
        BackColor = backColor;
    }

    public string Content { get; set; }
    public ConsoleColor? ForeColor { get; set; }
    public ConsoleColor? BackColor { get; set; }
}