namespace TicketManagement.Api.Exceptions;

// Maps to HTTP 409 - the ticket was modified by someone else since it was read (section 3).
public class ConcurrencyConflictException(Guid ticketId)
    : Exception($"Ticket '{ticketId}' was modified by another request. Reload and try again.")
{
    public Guid TicketId { get; } = ticketId;
}
