namespace left_4_bot.ServerLogs;

/// <summary>
/// A newly appended UTF-8 text range from one active server log file.
/// </summary>
/// <param name="FilePath">Local path of the log file the slice was read from.</param>
/// <param name="Text">Decoded text for the new byte range.</param>
public sealed record ServerLogTailSlice(string FilePath, string Text);
