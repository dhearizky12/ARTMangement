using System.Net;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Shared table-based email layout for ALL Bantu-Bantu mail.
/// Inline CSS only (mail clients strip style blocks); max width 600px; the
/// hard offset shadow is emulated with an ink wrapper cell because box-shadow
/// is unreliable across clients; explicit backgrounds keep dark-mode
/// inversion at AA contrast; no images, no tracking pixels.</summary>
public static class EmailLayout
{
    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Build(string subject, string preheader, string headline, string bodyHtml, string? buttonText, string? buttonUrl, string whyLine, string replyTo)
    {
        var button = string.IsNullOrWhiteSpace(buttonText) || string.IsNullOrWhiteSpace(buttonUrl) ? "" :
            $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:16px 0 8px 0;""><tr>" +
            $@"<td align=""center"" style=""background:{EmailTheme.Primary};border:{EmailTheme.BorderWidth} solid {EmailTheme.Ink};border-radius:{EmailTheme.Radius};"">" +
            $@"<a href=""{Escape(buttonUrl)}"" style=""display:inline-block;padding:12px 24px;color:{EmailTheme.Surface};text-decoration:none;font-family:{EmailTheme.FontFamily};font-weight:700;font-size:16px;"">{Escape(buttonText)}</a>" +
            $@"</td></tr></table>" +
            $@"<p style=""margin:0 0 16px 0;font-family:{EmailTheme.FontFamily};font-size:13px;color:{EmailTheme.Muted};word-break:break-all;"">Tautan: {Escape(buttonUrl)}</p>";
        var support = string.IsNullOrWhiteSpace(replyTo) ? "" :
            $@"<p style=""margin:8px 0 0 0;font-family:{EmailTheme.FontFamily};font-size:13px;color:{EmailTheme.Muted};"">Butuh bantuan? Balas ke {Escape(replyTo)}.</p>";
        return $@"<!DOCTYPE html><html lang=""id""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><meta name=""color-scheme"" content=""light""><title>{Escape(subject)}</title></head>" +
        $@"<body style=""margin:0;padding:0;background:{EmailTheme.PageBackground};"">" +
        $@"<span style=""display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;"">{Escape(preheader)}</span>" +
        $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{EmailTheme.PageBackground};""><tr><td align=""center"" style=""padding:24px 12px;"">" +
        $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;max-width:600px;"">" +
        $@"<tr><td style=""background:{EmailTheme.Primary};color:{EmailTheme.Surface};padding:16px 24px;font-family:{EmailTheme.FontFamily};font-weight:700;font-size:20px;border:{EmailTheme.BorderWidth} solid {EmailTheme.Ink};border-bottom:none;border-radius:{EmailTheme.Radius} {EmailTheme.Radius} 0 0;"">{Escape(EmailTheme.Wordmark)}</td></tr>" +
        $@"<tr><td style=""background:{EmailTheme.Ink};padding:0 {EmailTheme.ShadowOffset} {EmailTheme.ShadowOffset} 0;"">" +
        $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{EmailTheme.Surface};border:{EmailTheme.BorderWidth} solid {EmailTheme.Ink};border-radius:{EmailTheme.Radius};""><tr><td style=""padding:24px;"">" +
        $@"<h1 style=""margin:0 0 16px 0;font-family:{EmailTheme.FontFamily};font-weight:700;font-size:22px;color:{EmailTheme.Ink};"">{Escape(headline)}</h1>" +
        bodyHtml + button +
        $@"<hr style=""border:none;border-top:2px solid {EmailTheme.Ink};margin:24px 0 16px 0;"">" +
        $@"<p style=""margin:0;font-family:{EmailTheme.FontFamily};font-size:13px;color:{EmailTheme.Muted};"">{Escape(whyLine)}</p>" + support +
        $@"</td></tr></table></td></tr></table>" +
        $@"</td></tr></table></td></tr></table></body></html>";
    }
    public static string Paragraph(string text) =>
        $@"<p style=""margin:0 0 12px 0;font-family:{EmailTheme.FontFamily};font-size:15px;line-height:1.5;color:{EmailTheme.Ink};"">{Escape(text)}</p>";
    public static string Facts(params (string Label, string Value)[] rows)
    {
        var cells = string.Concat(rows.Select(r =>
            $@"<tr><td style=""padding:6px 12px 6px 0;font-family:{EmailTheme.FontFamily};font-size:14px;color:{EmailTheme.Muted};vertical-align:top;white-space:nowrap;"">{Escape(r.Label)}</td>" +
            $@"<td style=""padding:6px 0;font-family:{EmailTheme.FontFamily};font-size:14px;font-weight:700;color:{EmailTheme.Ink};"">{Escape(r.Value)}</td></tr>"));
        return $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:4px 0 16px 0;"">{cells}</table>";
    }
}
