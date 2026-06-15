using System.Text.RegularExpressions;

namespace RhinoKeyboardWedge.App.Services;

internal sealed class QuantityParser
{
    private static readonly Regex IntegerRegex = new(
        @"^\d+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public bool TryParse(string raw, string pattern, out string quantity, out string error)
    {
        quantity = string.Empty;
        error = string.Empty;

        try
        {
            var match = Regex.Match(
                raw,
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline,
                TimeSpan.FromMilliseconds(250));

            if (!match.Success)
            {
                error = "No se encontro QTY en la trama.";
                return false;
            }

            if (match.Groups.Count < 2)
            {
                error = "El regex debe incluir un grupo de captura para el número.";
                return false;
            }

            var value = match.Groups[1].Value.Trim();
            if (!IntegerRegex.IsMatch(value))
            {
                error = "El valor capturado no es un entero.";
                return false;
            }

            quantity = value;
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            error = "El regex excedio el tiempo maximo de evaluacion.";
            return false;
        }
        catch (ArgumentException exception)
        {
            error = $"Regex invalido: {exception.Message}";
            return false;
        }
    }
}
