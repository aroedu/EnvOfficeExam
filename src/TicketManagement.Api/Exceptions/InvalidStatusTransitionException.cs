namespace TicketManagement.Api.Exceptions;

// Maps to HTTP 400/409 - the requested status transition is not allowed (section 2).
public class InvalidStatusTransitionException(Models.TicketStatus from, Models.TicketStatus to)
    : Exception($"Transition from '{from}' to '{to}' is not allowed.")
{
    public Models.TicketStatus From { get; } = from;
    public Models.TicketStatus To { get; } = to;
}
