using TicketManagement.Api.Models;

namespace TicketManagement.Api.Dtos;

public record TicketDto(
    Guid Id,
    string Title,
    string OrganizationName,
    TicketStatus Status,
    TicketPriority Priority,
    string? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version)
{
    public static TicketDto FromEntity(Ticket ticket) => new(
        ticket.Id,
        ticket.Title,
        ticket.OrganizationName,
        ticket.Status,
        ticket.Priority,
        ticket.AssignedTo,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.Version);
}

public record CreateTicketRequest(
    string Title,
    string OrganizationName,
    TicketPriority Priority,
    string? AssignedTo);

// Version must match the current row so the update can be rejected on conflict (section 3).
public record UpdateTicketStatusRequest(TicketStatus NewStatus, int Version);

public record BulkStatusUpdateItem(Guid TicketId, TicketStatus NewStatus, int Version);

public record BulkStatusUpdateItemResult(Guid TicketId, bool Success, string? Error);

public record TicketAuditLogDto(TicketStatus OldStatus, TicketStatus NewStatus, string? ChangedBy, DateTimeOffset ChangedAt);
