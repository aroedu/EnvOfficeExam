using Microsoft.EntityFrameworkCore;
using TicketManagement.Api.Data;
using TicketManagement.Api.Dtos;
using TicketManagement.Api.Exceptions;
using TicketManagement.Api.Models;

namespace TicketManagement.Api.Services;

public class TicketService(AppDbContext db) : ITicketService
{
    // Section 1: paging, combined filtering, text search, sorting by at least three fields.
    public async Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQueryParameters query, CancellationToken cancellationToken)
    {
        // AsNoTracking + server-side query so the full data set is never pulled into memory.
        IQueryable<Ticket> tickets = db.Tickets.AsNoTracking();

        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            tickets = tickets.Where(t => t.AssignedTo == query.AssignedTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            tickets = tickets.Where(t =>
                EF.Functions.ILike(t.Title, pattern) ||
                EF.Functions.ILike(t.OrganizationName, pattern));
        }

        IOrderedQueryable<Ticket> sortedTickets = (query.SortBy, query.SortDescending) switch
        {
            (TicketSortField.CreatedAt, true) => tickets.OrderByDescending(t => t.CreatedAt),
            (TicketSortField.CreatedAt, false) => tickets.OrderBy(t => t.CreatedAt),
            (TicketSortField.UpdatedAt, true) => tickets.OrderByDescending(t => t.UpdatedAt),
            (TicketSortField.UpdatedAt, false) => tickets.OrderBy(t => t.UpdatedAt),
            (TicketSortField.Priority, true) => tickets.OrderByDescending(t => t.Priority),
            (TicketSortField.Priority, false) => tickets.OrderBy(t => t.Priority),
            (TicketSortField.Title, true) => tickets.OrderByDescending(t => t.Title),
            (TicketSortField.Title, false) => tickets.OrderBy(t => t.Title),
            (TicketSortField.Status, true) => tickets.OrderByDescending(t => t.Status),
            (TicketSortField.Status, false) => tickets.OrderBy(t => t.Status),
            _ => tickets.OrderByDescending(t => t.CreatedAt)
        };
        // Tie-break by Id so paging is stable across pages.
        var orderedTickets = sortedTickets.ThenBy(t => t.Id);

        var totalCount = await tickets.CountAsync(cancellationToken);

        var items = await orderedTickets
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => TicketDto.FromEntity(t))
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TicketDto> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new TicketNotFoundException(id);

        return TicketDto.FromEntity(ticket);
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket
        {
            Title = request.Title,
            OrganizationName = request.OrganizationName,
            Priority = request.Priority,
            AssignedTo = request.AssignedTo,
            Status = TicketStatus.New,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return TicketDto.FromEntity(ticket);
    }

    // Section 2 (invalid transition / not found) + section 3 (optimistic concurrency) + section 4 (audit log).
    public async Task<TicketDto> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new TicketNotFoundException(id);

        if (!TicketStatusTransitions.IsAllowed(ticket.Status, request.NewStatus))
        {
            throw new InvalidStatusTransitionException(ticket.Status, request.NewStatus);
        }

        // Tell EF what version the client last saw; SaveChanges fails if the row moved on.
        db.Entry(ticket).Property(t => t.Version).OriginalValue = request.Version;

        var oldStatus = ticket.Status;
        ticket.Status = request.NewStatus;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.Version++;

        db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            OldStatus = oldStatus,
            NewStatus = ticket.Status,
            ChangedAt = ticket.UpdatedAt
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(id);
        }

        return TicketDto.FromEntity(ticket);
    }

    // Section 4: batch operation where individual items may not exist or may fail without aborting the rest.
    public async Task<IReadOnlyList<BulkStatusUpdateItemResult>> BulkUpdateStatusAsync(
        IReadOnlyList<BulkStatusUpdateItem> items, CancellationToken cancellationToken)
    {
        var results = new List<BulkStatusUpdateItemResult>(items.Count);

        foreach (var item in items)
        {
            try
            {
                await UpdateStatusAsync(item.TicketId, new UpdateTicketStatusRequest(item.NewStatus, item.Version), cancellationToken);
                results.Add(new BulkStatusUpdateItemResult(item.TicketId, true, null));
            }
            catch (TicketNotFoundException ex)
            {
                results.Add(new BulkStatusUpdateItemResult(item.TicketId, false, ex.Message));
            }
            catch (InvalidStatusTransitionException ex)
            {
                results.Add(new BulkStatusUpdateItemResult(item.TicketId, false, ex.Message));
            }
            catch (ConcurrencyConflictException ex)
            {
                results.Add(new BulkStatusUpdateItemResult(item.TicketId, false, ex.Message));
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<TicketAuditLogDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await db.Tickets.AsNoTracking().AnyAsync(t => t.Id == id, cancellationToken);
        if (!exists)
        {
            throw new TicketNotFoundException(id);
        }

        return await db.TicketAuditLogs
            .AsNoTracking()
            .Where(a => a.TicketId == id)
            .OrderByDescending(a => a.ChangedAt)
            .Select(a => new TicketAuditLogDto(a.OldStatus, a.NewStatus, a.ChangedBy, a.ChangedAt))
            .ToListAsync(cancellationToken);
    }
}
