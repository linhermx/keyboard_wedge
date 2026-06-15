namespace LinherKeyboardWedge.App.Services;

internal sealed class ProcessedReadingEventArgs : EventArgs
{
    public ProcessedReadingEventArgs(
        DateTime timestamp,
        string raw,
        string? quantity,
        ReadingResult result,
        string message)
    {
        Timestamp = timestamp;
        Raw = raw;
        Quantity = quantity;
        Result = result;
        Message = message;
    }

    public DateTime Timestamp { get; }

    public string Raw { get; }

    public string? Quantity { get; }

    public ReadingResult Result { get; }

    public string Message { get; }
}

internal enum ReadingResult
{
    Sent,
    IgnoredDuplicate,
    Error
}
