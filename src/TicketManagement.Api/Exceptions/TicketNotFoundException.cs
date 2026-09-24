namespace TicketManagement.Api.Exceptions;

// Maps to HTTP 404 - requested ticket does not exist (section 2).
public class TicketNotFoundException(Guid ticketId)
    : Exception($"Ticket '{ticketId}' was not found.")
{
    public Guid TicketId { get; } = ticketId;
}
