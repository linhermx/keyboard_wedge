using RhinoKeyboardWedge.App.Configuration;
using RhinoKeyboardWedge.App.Logging;

namespace RhinoKeyboardWedge.App.Services;

internal sealed class WedgeProcessor
{
    private readonly object _sync = new();
    private readonly QuantityParser _parser;
    private readonly IKeyboardSender _keyboardSender;
    private readonly DailyFileLogger _logger;
    private AppSettings _settings;
    private string? _lastQuantity;
    private DateTime _lastSentUtc = DateTime.MinValue;

    public WedgeProcessor(AppSettings settings, QuantityParser parser, IKeyboardSender keyboardSender, DailyFileLogger logger)
    {
        _settings = settings;
        _parser = parser;
        _keyboardSender = keyboardSender;
        _logger = logger;
    }

    public event EventHandler<ProcessedReadingEventArgs>? ReadingProcessed;

    public void UpdateSettings(AppSettings settings)
    {
        lock (_sync)
        {
            _settings = settings;
        }
    }

    public void ProcessFrame(string raw)
    {
        lock (_sync)
        {
            var timestamp = DateTime.Now;

            if (!_parser.TryParse(raw, _settings.QuantityRegex, out var quantity, out var parseError))
            {
                _logger.LogReading(timestamp, raw, null, $"ERROR=\"{parseError}\"");
                OnReadingProcessed(new ProcessedReadingEventArgs(timestamp, raw, null, ReadingResult.Error, parseError));
                return;
            }

            var nowUtc = DateTime.UtcNow;
            if (_lastQuantity == quantity &&
                (nowUtc - _lastSentUtc).TotalMilliseconds < Math.Max(0, _settings.DuplicateWindowMs))
            {
                _logger.LogReading(timestamp, raw, quantity, "IGNORED_DUPLICATE");
                OnReadingProcessed(new ProcessedReadingEventArgs(
                    timestamp,
                    raw,
                    quantity,
                    ReadingResult.IgnoredDuplicate,
                    "Ignorado por duplicado."));
                return;
            }

            try
            {
                _keyboardSender.SendText(quantity, _settings.PostSendAction);
                _lastQuantity = quantity;
                _lastSentUtc = nowUtc;

                _logger.LogReading(timestamp, raw, quantity, "SENT");
                OnReadingProcessed(new ProcessedReadingEventArgs(
                    timestamp,
                    raw,
                    quantity,
                    ReadingResult.Sent,
                    "Enviado."));
            }
            catch (Exception exception)
            {
                _logger.LogReading(timestamp, raw, quantity, $"ERROR=\"{exception.Message}\"");
                OnReadingProcessed(new ProcessedReadingEventArgs(
                    timestamp,
                    raw,
                    quantity,
                    ReadingResult.Error,
                    exception.Message));
            }
        }
    }

    private void OnReadingProcessed(ProcessedReadingEventArgs args)
    {
        ReadingProcessed?.Invoke(this, args);
    }
}
