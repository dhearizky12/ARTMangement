using System.Text;
using System.Text.Json;
using BantuBantu.Application;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Renders every notification template. Pure function of
/// (templateKey, payload, baseUrl, replyTo). All interpolated values are
/// HTML-escaped; every mail carries a plain-text part.</summary>
public class NotificationRenderer(AppSettings app, EmailSettings email) : INotificationRenderer
{
    private static string Str(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }
    private static string[] StrArray(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray() : [];
    private static int Num(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
    private string Url(string path) => string.IsNullOrWhiteSpace(app.FrontendBaseUrl) ? "" : app.FrontendBaseUrl + path;
    private static string StatusLabel(string status) => status switch
    {
        "Confirmed" => "dikonfirmasi",
        "Completed" => "selesai",
        "Cancelled" => "dibatalkan",
        _ => "menunggu konfirmasi"
    };
    public RenderedEmail Render(string templateKey, JsonElement payload) => templateKey switch
    {
        NotificationTemplates.OrderNewProvider => OrderNewProvider(payload),
        NotificationTemplates.OrderNewCustomer => OrderNewCustomer(payload),
        NotificationTemplates.OrderNewAdmin => OrderNewAdmin(payload),
        NotificationTemplates.OrderStatusProvider => OrderStatus(payload, forProvider: true),
        NotificationTemplates.OrderStatusCustomer => OrderStatus(payload, forProvider: false),
        NotificationTemplates.ReviewReceived => ReviewReceived(payload),
        NotificationTemplates.ApplicationSubmittedProvider => ApplicationSubmittedProvider(payload),
        NotificationTemplates.ApplicationSubmittedAdmin => ApplicationSubmittedAdmin(payload),
        NotificationTemplates.ApplicationApproved => ApplicationApproved(payload),
        NotificationTemplates.ApplicationRejected => ApplicationDecision(payload, rejected: true),
        NotificationTemplates.ApplicationNeedsChanges => ApplicationDecision(payload, rejected: false),
        NotificationTemplates.ProviderSuspended => ProviderSuspended(payload),
        NotificationTemplates.ProviderReactivated => ProviderReactivated(payload),
        NotificationTemplates.AgencySuspended => AgencyStatus(payload, suspended: true),
        NotificationTemplates.AgencyReactivated => AgencyStatus(payload, suspended: false),
        NotificationTemplates.PasswordChanged => PasswordChanged(payload),
        NotificationTemplates.AuthOtp => AuthOtp(payload),
        _ => throw new ArgumentException($"Unknown notification template: {templateKey}", nameof(templateKey))
    };
    private static string OrderFacts(JsonElement p)
    {
        var services = StrArray(p, "services");
        return EmailLayout.Facts(
            ("Layanan", services.Length == 0 ? "-" : string.Join(", ", services)),
            ("Tanggal", Str(p, "scheduledDate")),
            ("Wilayah", $"{Str(p, "village")}, {Str(p, "district")}"));
    }
    private RenderedEmail OrderNewProvider(JsonElement p)
    {
        var customer = Str(p, "customerFirstName");
        var subject = $"[Bantu-Bantu] Pesanan baru dari {customer}";
        var html = EmailLayout.Paragraph($"Halo {customer} memesan layanan Anda. Berikut ringkasannya:") + OrderFacts(p);
        var text = $"Halo, {customer} memesan layanan Anda.\nLayanan: {string.Join(", ", StrArray(p, "services"))}\nTanggal: {Str(p, "scheduledDate")}\nWilayah: {Str(p, "village")}, {Str(p, "district")}";
        return Finish(subject, subject, "Pesanan baru untuk Anda.", html, text, "Lihat pesanan", Url("/provider/dashboard?tab=pesanan"),
            "Anda menerima email ini karena pesanan ini ditujukan kepada Anda di Bantu-Bantu.");
    }
    private RenderedEmail OrderNewCustomer(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Pesanan Anda diterima";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "customerFirstName")}, pesanan Anda sudah diteruskan ke {Str(p, "providerFirstName")}.") + OrderFacts(p) +
            EmailLayout.Paragraph("Penyedia akan dikabari dan status pesanan dapat Anda pantau di halaman pesanan.");
        var text = $"Halo {Str(p, "customerFirstName")}, pesanan Anda sudah diteruskan ke {Str(p, "providerFirstName")}.\nLayanan: {string.Join(", ", StrArray(p, "services"))}\nTanggal: {Str(p, "scheduledDate")}\nWilayah: {Str(p, "village")}, {Str(p, "district")}";
        return Finish(subject, subject, "Pesanan Anda sudah diteruskan.", html, text, "Lihat pesanan saya", Url("/orders"),
            "Anda menerima email ini karena Anda membuat pesanan di Bantu-Bantu.");
    }
    private RenderedEmail OrderNewAdmin(JsonElement p)
    {
        var subject = $"[Bantu-Bantu] Pesanan baru untuk {Str(p, "providerFirstName")}";
        var html = EmailLayout.Paragraph($"Pesanan baru dari {Str(p, "customerFirstName")} untuk {Str(p, "providerFirstName")}.") + OrderFacts(p);
        var text = $"Pesanan baru dari {Str(p, "customerFirstName")} untuk {Str(p, "providerFirstName")}.\nTanggal: {Str(p, "scheduledDate")}\nWilayah: {Str(p, "village")}, {Str(p, "district")}";
        return Finish(subject, subject, "Pesanan baru masuk.", html, text, "Kelola pesanan", Url("/admin/orders"),
            "Anda menerima email ini karena Anda mengelola pesanan di Bantu-Bantu.");
    }
    private RenderedEmail OrderStatus(JsonElement p, bool forProvider)
    {
        var status = StatusLabel(Str(p, "status"));
        var other = forProvider ? Str(p, "customerFirstName") : Str(p, "providerFirstName");
        var subject = $"[Bantu-Bantu] Pesanan {status}";
        var headline = $"Pesanan {status}.";
        var html = EmailLayout.Paragraph(forProvider
            ? $"Pesanan dari {other} telah {status}. Berikut ringkasannya:"
            : $"Pesanan Anda dengan {other} telah {status}. Berikut ringkasannya:") + OrderFacts(p);
        var text = $"Pesanan telah {status}.\nLayanan: {string.Join(", ", StrArray(p, "services"))}\nTanggal: {Str(p, "scheduledDate")}\nWilayah: {Str(p, "village")}, {Str(p, "district")}";
        var (button, url) = forProvider
            ? ("Lihat pesanan", Url("/provider/dashboard?tab=pesanan"))
            : ("Lihat pesanan saya", Url("/orders"));
        var why = forProvider
            ? "Anda menerima email ini karena pesanan ini ditujukan kepada Anda di Bantu-Bantu."
            : "Anda menerima email ini karena Anda membuat pesanan di Bantu-Bantu.";
        return Finish(subject, subject, headline, html, text, button, url, why);
    }
    private RenderedEmail ReviewReceived(JsonElement p)
    {
        var rating = Num(p, "rating");
        var subject = $"[Bantu-Bantu] Ulasan baru bintang {rating}";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, pelanggan memberi ulasan bintang {rating} untuk pesanan Anda.") +
            EmailLayout.Paragraph("Buka dasbor untuk melihat detail ulasan.");
        var text = $"Halo {Str(p, "providerFirstName")}, pelanggan memberi ulasan bintang {rating} untuk pesanan Anda.";
        return Finish(subject, subject, $"Ulasan baru: {rating} dari 5.", html, text, "Lihat dasbor", Url("/provider/dashboard"),
            "Anda menerima email ini karena Anda menerima ulasan di Bantu-Bantu.");
    }
    private RenderedEmail ApplicationSubmittedProvider(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Aplikasi Anda terkirim";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, aplikasi provider Anda sudah terkirim dan sedang ditinjau admin.") +
            EmailLayout.Paragraph("Kami akan mengabari melalui email ini setiap ada perkembangan.");
        var text = $"Halo {Str(p, "providerFirstName")}, aplikasi provider Anda sudah terkirim dan sedang ditinjau admin.";
        return Finish(subject, subject, "Aplikasi terkirim.", html, text, "Lihat aplikasi", Url("/provider/onboarding"),
            "Anda menerima email ini karena Anda mengajukan aplikasi provider di Bantu-Bantu.");
    }
    private RenderedEmail ApplicationSubmittedAdmin(JsonElement p)
    {
        var subject = $"[Bantu-Bantu] Aplikasi baru dari {Str(p, "providerFirstName")}";
        var html = EmailLayout.Paragraph($"Aplikasi provider baru dari {Str(p, "providerFirstName")} menunggu peninjauan.");
        var text = $"Aplikasi provider baru dari {Str(p, "providerFirstName")} menunggu peninjauan.";
        return Finish(subject, subject, "Aplikasi baru masuk.", html, text, "Tinjau aplikasi", Url("/admin/providers"),
            "Anda menerima email ini karena Anda meninjau aplikasi provider di Bantu-Bantu.");
    }
    private RenderedEmail ApplicationApproved(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Aplikasi Anda disetujui";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, selamat! Aplikasi provider Anda telah disetujui dan profil Anda sudah tayang.") +
            EmailLayout.Paragraph("Lengkapi ketersediaan mingguan agar pelanggan dapat memesan.");
        var text = $"Halo {Str(p, "providerFirstName")}, aplikasi provider Anda telah disetujui dan profil Anda sudah tayang.";
        return Finish(subject, subject, "Aplikasi disetujui.", html, text, "Buka dasbor", Url("/provider/dashboard"),
            "Anda menerima email ini karena Anda mengajukan aplikasi provider di Bantu-Bantu.");
    }
    private RenderedEmail ApplicationDecision(JsonElement p, bool rejected)
    {
        var note = Str(p, "note");
        var subject = rejected ? "[Bantu-Bantu] Aplikasi belum disetujui" : "[Bantu-Bantu] Lengkapi aplikasi Anda";
        var headline = rejected ? "Aplikasi belum disetujui." : "Aplikasi perlu dilengkapi.";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, " + (rejected
            ? "mohon maaf, aplikasi provider Anda belum dapat disetujui."
            : "aplikasi Anda perlu dilengkapi sebelum dapat disetujui.")) +
            (note.Length == 0 ? "" : EmailLayout.Facts(("Catatan admin", note)));
        var text = $"Halo {Str(p, "providerFirstName")}, " + (rejected
            ? "mohon maaf, aplikasi provider Anda belum dapat disetujui."
            : "aplikasi Anda perlu dilengkapi sebelum dapat disetujui.") + (note.Length == 0 ? "" : $"\nCatatan admin: {note}");
        return Finish(subject, subject, headline, html, text, "Perbaiki aplikasi", Url("/provider/onboarding"),
            "Anda menerima email ini karena Anda mengajukan aplikasi provider di Bantu-Bantu.");
    }
    private RenderedEmail ProviderSuspended(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Akun Anda ditangguhkan";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, akun provider Anda untuk sementara ditangguhkan.") +
            EmailLayout.Facts(("Alasan", Str(p, "note")));
        var text = $"Halo {Str(p, "providerFirstName")}, akun provider Anda untuk sementara ditangguhkan.\nAlasan: {Str(p, "note")}";
        return Finish(subject, subject, "Akun ditangguhkan.", html, text, "Buka dasbor", Url("/provider/dashboard"),
            "Anda menerima email ini karena status akun provider Anda berubah.");
    }
    private RenderedEmail ProviderReactivated(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Akun Anda aktif kembali";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "providerFirstName")}, kabar baik! Akun provider Anda sudah aktif kembali dan profil Anda tayang lagi.");
        var text = $"Halo {Str(p, "providerFirstName")}, akun provider Anda sudah aktif kembali.";
        return Finish(subject, subject, "Akun aktif kembali.", html, text, "Buka dasbor", Url("/provider/dashboard"),
            "Anda menerima email ini karena status akun provider Anda berubah.");
    }
    private RenderedEmail AgencyStatus(JsonElement p, bool suspended)
    {
        var name = Str(p, "agencyName");
        var subject = suspended ? $"[Bantu-Bantu] Agency {name} ditangguhkan" : $"[Bantu-Bantu] Agency {name} aktif kembali";
        var headline = suspended ? "Agency ditangguhkan." : "Agency aktif kembali.";
        var html = EmailLayout.Paragraph(suspended
            ? $"Agency {name} untuk sementara ditangguhkan. Provider di bawah agency ini tidak tampil di katalog selama penangguhan."
            : $"Agency {name} sudah aktif kembali.");
        var text = suspended
            ? $"Agency {name} untuk sementara ditangguhkan."
            : $"Agency {name} sudah aktif kembali.";
        return Finish(subject, subject, headline, html, text, "Kelola agency", Url("/admin/agencies"),
            "Anda menerima email ini karena Anda mengelola agency ini di Bantu-Bantu.");
    }
    private RenderedEmail PasswordChanged(JsonElement p)
    {
        var subject = "[Bantu-Bantu] Kata sandi Anda diubah";
        var html = EmailLayout.Paragraph($"Halo {Str(p, "name")}, kata sandi akun provider Anda baru saja diubah.") +
            EmailLayout.Paragraph("Abaikan email ini jika Anda yang melakukannya. Jika bukan Anda, segera hubungi admin.");
        var text = $"Halo {Str(p, "name")}, kata sandi akun provider Anda baru saja diubah. Abaikan jika Anda yang melakukannya.";
        return Finish(subject, subject, "Kata sandi diubah.", html, text, "Masuk", Url("/provider/login"),
            "Anda menerima email ini karena keamanan akun Anda berubah.");
    }
    private RenderedEmail AuthOtp(JsonElement p)
    {
        var subject = "Kode masuk Bantu-Bantu";
        var code = Str(p, "code");
        var ttl = Str(p, "ttlMinutes");
        var html = EmailLayout.Paragraph("Halo,") +
            EmailLayout.Paragraph("Gunakan kode berikut untuk masuk ke akun Bantu-Bantu:") +
            $@"<p style=""margin:0 0 12px 0;font-family:{EmailTheme.FontFamily};font-size:32px;letter-spacing:8px;font-weight:700;color:{EmailTheme.Ink};"">{EmailLayout.Escape(code)}</p>" +
            EmailLayout.Paragraph($"Berlaku {ttl} menit.") +
            EmailLayout.Paragraph("Jangan bagikan kode ini kepada siapa pun.") +
            EmailLayout.Paragraph("Abaikan email ini jika bukan Anda yang memintanya.");
        var text = $"Halo,\nGunakan kode berikut untuk masuk ke akun Bantu-Bantu:\n{code}\nBerlaku {ttl} menit.\nJangan bagikan kode ini kepada siapa pun.\nAbaikan email ini jika bukan Anda yang memintanya.";
        return Finish(subject, subject, "Kode masuk Anda.", html, text, null, null,
            "Anda menerima email ini karena ada upaya masuk ke akun Bantu-Bantu dengan alamat ini.");
    }
    private RenderedEmail Finish(string subject, string preheader, string headline, string htmlBody, string textBody, string? buttonText, string? buttonUrl, string why)
    {
        var html = EmailLayout.Build(subject, preheader, headline, htmlBody,
            string.IsNullOrWhiteSpace(buttonUrl) ? null : buttonText,
            string.IsNullOrWhiteSpace(buttonUrl) ? null : buttonUrl, why, email.ReplyTo);
        var text = new StringBuilder(headline).Append("\n\n").Append(textBody);
        if (!string.IsNullOrWhiteSpace(buttonUrl)) text.Append($"\n\n{buttonText}: {buttonUrl}");
        text.Append($"\n\n{why}");
        if (!string.IsNullOrWhiteSpace(email.ReplyTo)) text.Append($"\nButuh bantuan? Balas ke {email.ReplyTo}.");
        return new RenderedEmail(subject, html, text.ToString());
    }
    public IReadOnlyList<(string TemplateKey, string PayloadJson)> PreviewSamples()
    {
        var order = NotificationJson.Serialize(new OrderMailPayload(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Budi", "Sari", ["Asisten rumah tangga", "Kebersihan"], "12 Januari 2026", "Kelurahan Sukamaju", "Kecamatan Cilodong", "Confirmed"));
        var app = NotificationJson.Serialize(new ApplicationMailPayload(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Sari", "Foto KTP kurang jelas, mohon unggah ulang."));
        var appPlain = NotificationJson.Serialize(new ApplicationMailPayload(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Sari"));
        var agency = NotificationJson.Serialize(new AgencyMailPayload(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Agency Maju Bersama"));
        return
        [
            (NotificationTemplates.OrderNewProvider, order),
            (NotificationTemplates.OrderNewCustomer, order),
            (NotificationTemplates.OrderNewAdmin, order),
            (NotificationTemplates.OrderStatusProvider, order),
            (NotificationTemplates.OrderStatusCustomer, order),
            (NotificationTemplates.ReviewReceived, NotificationJson.Serialize(new ReviewMailPayload(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Sari", 5))),
            (NotificationTemplates.ApplicationSubmittedProvider, appPlain),
            (NotificationTemplates.ApplicationSubmittedAdmin, appPlain),
            (NotificationTemplates.ApplicationApproved, appPlain),
            (NotificationTemplates.ApplicationRejected, app),
            (NotificationTemplates.ApplicationNeedsChanges, app),
            (NotificationTemplates.ProviderSuspended, app),
            (NotificationTemplates.ProviderReactivated, appPlain),
            (NotificationTemplates.AgencySuspended, agency),
            (NotificationTemplates.AgencyReactivated, agency),
            (NotificationTemplates.PasswordChanged, NotificationJson.Serialize(new PasswordMailPayload("Sari"))),
            (NotificationTemplates.AuthOtp, NotificationJson.Serialize(new OtpMailPayload("481516", 10))),
        ];
    }
}
