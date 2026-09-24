using TicketManagement.Api.Dtos;

namespace TicketManagement.Api.Services;

public interface ITicketService
{
    Task<TicketStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken);

    Task<PagedResult<TicketDto>> GetTicketsAsync(TicketQueryParameters query, CancellationToken cancellationToken);

    Task<TicketDto> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TicketDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken);

    Task<TicketDto> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BulkStatusUpdateItemResult>> BulkUpdateStatusAsync(
        IReadOnlyList<BulkStatusUpdateItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketAuditLogDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken);
}
