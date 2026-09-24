namespace TicketManagement.Api.Models;

// Audit trail entry recorded on every status change (section 4).
public class TicketAuditLog
{
    public long Id { get; set; }

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    public TicketStatus OldStatus { get; set; }

    public TicketStatus NewStatus { get; set; }

    public string? ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
