using System.IO.Ports;

namespace RhinoKeyboardWedge.App.Services;

internal static class SerialOptionParser
{
    public static Parity ParseParity(string value)
    {
        return Enum.TryParse(value, true, out Parity parsed)
            ? parsed
            : Parity.None;
    }

    public static StopBits ParseStopBits(string value)
    {
        return value.Trim() switch
        {
            "1" => StopBits.One,
            "1.5" => StopBits.OnePointFive,
            "2" => StopBits.Two,
            _ when Enum.TryParse(value, true, out StopBits parsed) => parsed,
            _ => StopBits.One
        };
    }

    public static Handshake ParseHandshake(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NONE" => Handshake.None,
            "XONXOFF" => Handshake.XOnXOff,
            "RTS" => Handshake.RequestToSend,
            "REQUESTTOSEND" => Handshake.RequestToSend,
            "RTS/XONXOFF" => Handshake.RequestToSendXOnXOff,
            "REQUESTTOSENDXONXOFF" => Handshake.RequestToSendXOnXOff,
            _ when Enum.TryParse(value, true, out Handshake parsed) => parsed,
            _ => Handshake.None
        };
    }
}
