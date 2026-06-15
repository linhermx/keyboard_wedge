using System.IO.Ports;
using System.Text;
using RJCP.IO.Ports;
using RhinoKeyboardWedge.App.Configuration;
using RhinoKeyboardWedge.App.Logging;
using MsHandshake = System.IO.Ports.Handshake;
using MsParity = System.IO.Ports.Parity;
using MsSerialPort = System.IO.Ports.SerialPort;
using MsStopBits = System.IO.Ports.StopBits;
using RjcpHandshake = RJCP.IO.Ports.Handshake;
using RjcpParity = RJCP.IO.Ports.Parity;
using RjcpStopBits = RJCP.IO.Ports.StopBits;

namespace RhinoKeyboardWedge.App.Services;

internal sealed class ScaleSerialService : IDisposable
{
    private readonly object _sync = new();
    private readonly StringBuilder _lineBuffer = new();
    private readonly List<string> _frameLines = [];
    private readonly DailyFileLogger _logger;

    private ISerialConnection? _connection;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    public ScaleSerialService(DailyFileLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler<string>? FrameReceived;

    public event EventHandler<string>? StatusChanged;

    public bool IsConnected => _connection?.IsOpen == true;

    public static string[] GetAvailablePorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var port in MsSerialPort.GetPortNames())
        {
            ports.Add(port);
        }

        using (var serialPortStream = new SerialPortStream())
        {
            foreach (var port in serialPortStream.GetPortNames())
            {
                ports.Add(port);
            }
        }

        return ports.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void Connect(AppSettings settings)
    {
        lock (_sync)
        {
            DisconnectCore();

            var attemptedPorts = BuildCandidatePorts(settings.PortName);
            if (attemptedPorts.Count == 0)
            {
                throw new InvalidOperationException("No se detectaron puertos COM disponibles.");
            }

            var errors = new List<string>();
            foreach (var portName in attemptedPorts)
            {
                var candidateSettings = CloneWithPort(settings, portName);
                if (TryConnectWithBackends(candidateSettings, errors))
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"No se pudo conectar a ningun puerto. Intentados: {string.Join(", ", attemptedPorts)}. {string.Join(" | ", errors)}");
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            DisconnectCore();
        }
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void StartReadLoop()
    {
        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ISerialConnection? connection;

            lock (_sync)
            {
                connection = _connection;
            }

            if (connection is null || !connection.IsOpen)
            {
                return;
            }

            try
            {
                var data = connection.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    HandleIncomingText(data);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError("SERIAL_READ", exception);
                StatusChanged?.Invoke(this, $"Error serial: {exception.Message}");
                return;
            }

            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void DisconnectCore()
    {
        StopReadLoop();

        if (_connection is null)
        {
            return;
        }

        try
        {
            _connection.Close();
            _logger.LogInfo("SERIAL", "Disconnected");
            StatusChanged?.Invoke(this, "Desconectado");
        }
        finally
        {
            _connection.Dispose();
            _connection = null;
        }
    }

    private void CleanupConnectionOnly()
    {
        try
        {
            _connection?.Close();
        }
        catch
        {
        }
        finally
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    private void StopReadLoop()
    {
        var cts = _readLoopCts;
        var task = _readLoopTask;
        _readLoopCts = null;
        _readLoopTask = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            task?.Wait(250);
        }
        catch
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void LogConnected(AppSettings settings, string backendName)
    {
        _lineBuffer.Clear();
        _frameLines.Clear();
        _logger.LogInfo(
            "SERIAL",
            $"Connected backend={backendName} port={settings.PortName} baud={settings.BaudRate} dataBits={settings.DataBits} parity={settings.Parity} stopBits={settings.StopBits} handshake={settings.FlowControl} dtr={settings.DtrEnable} rts={settings.RtsEnable}");
        StatusChanged?.Invoke(this, $"Conectado a {settings.PortName} ({backendName})");
    }

    private void HandleIncomingText(string data)
    {
        lock (_sync)
        {
            var normalized = data.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            _lineBuffer.Append(normalized);

            var bufferText = _lineBuffer.ToString();
            var lastNewLine = bufferText.LastIndexOf('\n');
            if (lastNewLine < 0)
            {
                TrimOversizedBuffer();
                return;
            }

            var complete = bufferText[..lastNewLine];
            var remainder = bufferText[(lastNewLine + 1)..];

            _lineBuffer.Clear();
            _lineBuffer.Append(remainder);

            foreach (var rawLine in complete.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                _frameLines.Add(line);

                if (line.Contains("QTY:", StringComparison.OrdinalIgnoreCase))
                {
                    var frame = string.Join(Environment.NewLine, _frameLines);
                    _frameLines.Clear();
                    FrameReceived?.Invoke(this, frame);
                }
                else if (_frameLines.Count > 20)
                {
                    _frameLines.Clear();
                }
            }
        }
    }

    private void TrimOversizedBuffer()
    {
        const int maxBufferLength = 4096;
        if (_lineBuffer.Length > maxBufferLength)
        {
            _lineBuffer.Remove(0, _lineBuffer.Length - maxBufferLength);
        }
    }

    private static string BuildCombinedErrorMessage(string portName, Exception? systemException, Exception rjcpException)
    {
        var systemPart = systemException is null
            ? "System.IO.Ports: no intentado"
            : $"System.IO.Ports: {systemException.GetType().Name} 0x{systemException.HResult:X8} {systemException.Message}";
        var rjcpPart =
            $"RJCP.SerialPortStream: {rjcpException.GetType().Name} 0x{rjcpException.HResult:X8} {rjcpException.Message}";

        return $"No se pudo conectar al puerto {portName}. {systemPart}. {rjcpPart}.";
    }

    private bool TryConnectWithBackends(AppSettings settings, List<string> errors)
    {
        Exception? systemException = null;

        try
        {
            _connection = new SystemSerialConnection(settings);
            _connection.Open();
            StartReadLoop();
            LogConnected(settings, _connection.BackendName);
            return true;
        }
        catch (Exception exception)
        {
            systemException = exception;
            _logger.LogError("SERIAL_CONNECT_SYSTEM", exception);
            CleanupConnectionOnly();
        }

        try
        {
            _connection = new RjcpSerialConnection(settings);
            _connection.Open();
            StartReadLoop();
            LogConnected(settings, _connection.BackendName);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError("SERIAL_CONNECT_RJCP", exception);
            CleanupConnectionOnly();
            errors.Add(BuildCombinedErrorMessage(settings.PortName, systemException, exception));
            return false;
        }
    }

    private static List<string> BuildCandidatePorts(string preferredPort)
    {
        var ports = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(preferredPort) && seen.Add(preferredPort.Trim()))
        {
            ports.Add(preferredPort.Trim());
        }

        foreach (var port in GetAvailablePorts())
        {
            if (seen.Add(port))
            {
                ports.Add(port);
            }
        }

        return ports;
    }

    private static AppSettings CloneWithPort(AppSettings settings, string portName)
    {
        return new AppSettings
        {
            PortName = portName,
            BaudRate = settings.BaudRate,
            DataBits = settings.DataBits,
            Parity = settings.Parity,
            StopBits = settings.StopBits,
            FlowControl = settings.FlowControl,
            DtrEnable = settings.DtrEnable,
            RtsEnable = settings.RtsEnable,
            QuantityRegex = settings.QuantityRegex,
            PostSendAction = settings.PostSendAction,
            DuplicateWindowMs = settings.DuplicateWindowMs,
            StartMinimized = settings.StartMinimized,
            MinimizeToTray = settings.MinimizeToTray,
            StartWithWindows = settings.StartWithWindows
        };
    }

    private interface ISerialConnection : IDisposable
    {
        string BackendName { get; }

        bool IsOpen { get; }

        void Open();

        void Close();

        string ReadExisting();
    }

    private sealed class SystemSerialConnection : ISerialConnection
    {
        private readonly MsSerialPort _port;

        public SystemSerialConnection(AppSettings settings)
        {
            _port = new MsSerialPort
            {
                PortName = settings.PortName,
                BaudRate = settings.BaudRate,
                DataBits = settings.DataBits,
                Parity = ParseMsParity(settings.Parity),
                StopBits = ParseMsStopBits(settings.StopBits),
                Handshake = ParseMsHandshake(settings.FlowControl),
                DtrEnable = settings.DtrEnable,
                RtsEnable = settings.RtsEnable,
                Encoding = Encoding.ASCII,
                NewLine = "\n",
                ReadTimeout = 50,
                WriteTimeout = 500
            };
        }

        public string BackendName => "System.IO.Ports";

        public bool IsOpen => _port.IsOpen;

        public void Open()
        {
            _port.Open();
            TryResetBuffers();
        }

        public void Close()
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }

        public string ReadExisting()
        {
            return _port.ReadExisting();
        }

        public void Dispose()
        {
            _port.Dispose();
        }

        private void TryResetBuffers()
        {
            try
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }
            catch
            {
            }
        }
    }

    private sealed class RjcpSerialConnection : ISerialConnection
    {
        private readonly SerialPortStream _port;

        public RjcpSerialConnection(AppSettings settings)
        {
            _port = new SerialPortStream
            {
                PortName = settings.PortName,
                BaudRate = settings.BaudRate,
                DataBits = settings.DataBits,
                Parity = ParseRjcpParity(settings.Parity),
                StopBits = ParseRjcpStopBits(settings.StopBits),
                Handshake = ParseRjcpHandshake(settings.FlowControl),
                DtrEnable = settings.DtrEnable,
                RtsEnable = settings.RtsEnable,
                Encoding = Encoding.ASCII,
                NewLine = "\n",
                ReadTimeout = 50,
                WriteTimeout = 500
            };
        }

        public string BackendName => "RJCP.SerialPortStream";

        public bool IsOpen => _port.IsOpen;

        public void Open()
        {
            _port.OpenDirect();
        }

        public void Close()
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }

        public string ReadExisting()
        {
            return _port.ReadExisting();
        }

        public void Dispose()
        {
            _port.Dispose();
        }
    }

    private static MsParity ParseMsParity(string value)
    {
        return Enum.TryParse(value, true, out MsParity parsed) ? parsed : MsParity.None;
    }

    private static MsStopBits ParseMsStopBits(string value)
    {
        return value.Trim() switch
        {
            "1" => MsStopBits.One,
            "1.5" => MsStopBits.OnePointFive,
            "2" => MsStopBits.Two,
            _ when Enum.TryParse(value, true, out MsStopBits parsed) => parsed,
            _ => MsStopBits.One
        };
    }

    private static MsHandshake ParseMsHandshake(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NONE" => MsHandshake.None,
            "XONXOFF" => MsHandshake.XOnXOff,
            "RTS" => MsHandshake.RequestToSend,
            "REQUESTTOSEND" => MsHandshake.RequestToSend,
            "RTS/XONXOFF" => MsHandshake.RequestToSendXOnXOff,
            "REQUESTTOSENDXONXOFF" => MsHandshake.RequestToSendXOnXOff,
            _ when Enum.TryParse(value, true, out MsHandshake parsed) => parsed,
            _ => MsHandshake.None
        };
    }

    private static RjcpParity ParseRjcpParity(string value)
    {
        return Enum.TryParse(value, true, out RjcpParity parsed) ? parsed : RjcpParity.None;
    }

    private static RjcpStopBits ParseRjcpStopBits(string value)
    {
        return value.Trim() switch
        {
            "1" => RjcpStopBits.One,
            "1.5" => RjcpStopBits.One5,
            "2" => RjcpStopBits.Two,
            _ when Enum.TryParse(value, true, out RjcpStopBits parsed) => parsed,
            _ => RjcpStopBits.One
        };
    }

    private static RjcpHandshake ParseRjcpHandshake(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NONE" => RjcpHandshake.None,
            "XONXOFF" => RjcpHandshake.XOn,
            "RTS" => RjcpHandshake.Rts,
            "REQUESTTOSEND" => RjcpHandshake.Rts,
            "RTS/XONXOFF" => RjcpHandshake.RtsXOn,
            "REQUESTTOSENDXONXOFF" => RjcpHandshake.RtsXOn,
            _ when Enum.TryParse(value, true, out RjcpHandshake parsed) => parsed,
            _ => RjcpHandshake.None
        };
    }
}
