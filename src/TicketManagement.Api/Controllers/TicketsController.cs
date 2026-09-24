using Microsoft.AspNetCore.Mvc;
using TicketManagement.Api.Dtos;
using TicketManagement.Api.Services;

namespace TicketManagement.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    // Section 1: GET /api/tickets?page=&pageSize=&status=&priority=&assignedTo=&search=&sortBy=&sortDescending=
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketDto>>> GetTickets(
        [FromQuery] TicketQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await ticketService.GetTicketsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<TicketStatisticsDto>> GetStatistics(CancellationToken cancellationToken)
    {
        var statistics = await ticketService.GetStatisticsAsync(cancellationToken);
        return Ok(statistics);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetTicket(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetTicketByIdAsync(id, cancellationToken);
        return Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<TicketDto>> CreateTicket(
        [FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateTicketAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
    }

    // Section 2 (not found / invalid transition) + Section 3 (optimistic concurrency via Version).
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketDto>> UpdateStatus(
        Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(ticket);
    }

    // Section 4: bulk status update; individual items may fail (missing ticket, bad transition, stale version)
    // without failing the whole batch. Response lists a per-item outcome.
    [HttpPost("bulk-status")]
    public async Task<ActionResult<IReadOnlyList<BulkStatusUpdateItemResult>>> BulkUpdateStatus(
        [FromBody] IReadOnlyList<BulkStatusUpdateItem> items, CancellationToken cancellationToken)
    {
        if (items.Count > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bulk request exceeds the limit",
                Detail = "A maximum of 100 tickets can be updated per request."
            });
        }

        var results = await ticketService.BulkUpdateStatusAsync(items, cancellationToken);
        return Ok(results);
    }

    // Section 4: audit trail of every status change for a ticket.
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<TicketAuditLogDto>>> GetHistory(
        Guid id, CancellationToken cancellationToken)
    {
        var history = await ticketService.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
    }
}
