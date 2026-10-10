namespace BantuBantu.Domain;

// Transactional outbox for business emails. Rows are inserted in the SAME
// database transaction as the state change they announce, so a rolled-back
// change never sends mail. A BackgroundService drains Pending rows; the
// request path also makes one best-effort send right after commit.
public enum EmailOutboxStatus { Pending, Sent, Failed, Suppressed }
public class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // e.g. "order.status". Stable machine name, used for caps and dedupe.
    public string EventType { get; set; } = "";
    public string To { get; set; } = "";
    public Guid? UserId { get; set; }
    // event + entity id + state version + recipient. Unique: re-emitting the
    // same logical event is a no-op instead of a duplicate email.
    public string DedupeKey { get; set; } = "";
    // e.g. "order.status.customer". Selects subject/body in the renderer.
    public string TemplateKey { get; set; } = "";
    // Minimal JSON payload (names, ids, dates). Never full addresses, phones,
    // documents or ID numbers.
    public string PayloadJson { get; set; } = "{}";
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    // Sanitized (truncated, no secrets) last send error.
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}
