using System.Text;
using QRCoder;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Hilfen für die Einrichtung der Authenticator-App: lesbarer Schlüssel, <c>otpauth://</c>-Adresse und QR-Code.</summary>
public static class TotpSetup
{
    public const string Issuer = "Blutdruck-Tagebuch";

    /// <summary>Schlüssel in Vierergruppen (großgeschrieben), damit er sich vorlesen und abtippen lässt.</summary>
    public static string FormatKey(string sharedKey)
    {
        var clean = Normalize(sharedKey);
        var builder = new StringBuilder(clean.Length + clean.Length / 4);
        for (var i = 0; i < clean.Length; i += 4)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(clean, i, Math.Min(4, clean.Length - i));
        }

        return builder.ToString();
    }

    /// <summary><c>otpauth://totp/{issuer}:{email}?secret={Base32}&amp;issuer={issuer}</c>, URL-kodiert; ohne E-Mail nur mit dem Herausgeber.</summary>
    public static string BuildUri(string issuer, string? email, string sharedKey)
    {
        var label = string.IsNullOrWhiteSpace(email)
            ? Uri.EscapeDataString(issuer)
            : $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email.Trim())}";
        return $"otpauth://totp/{label}?secret={Normalize(sharedKey)}&issuer={Uri.EscapeDataString(issuer)}";
    }

    /// <summary>QR-Code als eingebettetes SVG (ohne Skript und ohne <c>style</c>-Attribute, damit die CSP greift).</summary>
    public static string QrSvg(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        // Heller Hintergrund und Ruhezone, damit der Code auch im dunklen Theme lesbar bleibt.
        return new SvgQRCode(data).GetGraphic(new System.Drawing.Size(1, 1), "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
