using TicketManagement.Api.Models;

namespace TicketManagement.Api.Dtos;

public enum TicketSortField
{
    CreatedAt,
    UpdatedAt,
    Priority,
    Title,
    Status
}

// Combined filter / search / sort / paging parameters for the tickets listing (section 1).
public class TicketQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? Math.Clamp(value, 1, MaxPageSize) : value;
    }

    public TicketStatus? Status { get; set; }

    public TicketPriority? Priority { get; set; }

    public string? AssignedTo { get; set; }

    // Free-text search over Title and OrganizationName.
    public string? Search { get; set; }

    public TicketSortField SortBy { get; set; } = TicketSortField.CreatedAt;

    public bool SortDescending { get; set; } = true;
}
