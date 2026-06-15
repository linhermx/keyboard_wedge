namespace LinherKeyboardWedge.App.Configuration;

internal sealed class AppSettings
{
    public string PortName { get; set; } = "COM6";

    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public string Parity { get; set; } = "None";

    public string StopBits { get; set; } = "1";

    public string FlowControl { get; set; } = "None";

    public bool DtrEnable { get; set; } = true;

    public bool RtsEnable { get; set; } = true;

    public string QuantityRegex { get; set; } = @"QTY:\s*(\d+)\s*pcs";

    public PostSendAction PostSendAction { get; set; } = PostSendAction.Enter;

    public int DuplicateWindowMs { get; set; } = 1000;

    public bool StartMinimized { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool StartWithWindows { get; set; }
}

internal enum PostSendAction
{
    None,
    Enter,
    Tab
}
