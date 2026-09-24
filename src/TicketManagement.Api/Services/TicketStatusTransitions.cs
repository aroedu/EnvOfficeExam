using TicketManagement.Api.Models;

namespace TicketManagement.Api.Services;

// Central state machine for allowed ticket status transitions (section 2).
public static class TicketStatusTransitions
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Allowed = new()
    {
        [TicketStatus.New] = [TicketStatus.InProgress],
        [TicketStatus.InProgress] = [TicketStatus.Waiting, TicketStatus.Completed],
        [TicketStatus.Waiting] = [TicketStatus.InProgress, TicketStatus.Completed],
        [TicketStatus.Completed] = []
    };

    public static bool IsAllowed(TicketStatus from, TicketStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }
}
