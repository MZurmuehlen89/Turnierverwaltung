namespace GuiTest.Framework;

/// <summary>Reads the end of an application log, e.g. to attach it to a failed test. The application usually
/// still holds the file open for writing, so it is opened with shared access.</summary>
public static class LogTail
{
    public static string Read(string path, int maxLines)
    {
        if (!File.Exists(path)) return $"(log not found: {path})";

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var tail = new Queue<string>(maxLines);
        while (reader.ReadLine() is { } line)
        {
            if (tail.Count == maxLines) tail.Dequeue();
            tail.Enqueue(line);
        }
        return string.Join(Environment.NewLine, tail);
    }
}
