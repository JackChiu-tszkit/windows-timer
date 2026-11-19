namespace WindowsTimer.Services;

public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsTimer",
        "logs");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "timer.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var detail = exception == null
            ? message
            : message + " | " + exception.GetType().Name + ": " + exception.Message;
        Write("ERROR", detail);
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    File.Copy(FilePath, FilePath + ".1", overwrite: true);
                    File.WriteAllText(FilePath, line);
                    return;
                }

                File.AppendAllText(FilePath, line);
            }
        }
        catch
        {
            // Logging must never take down the timer.
        }
    }
}
