using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TicketManagement.Api.Models;
using Xunit;

namespace TicketManagement.Api.IntegrationTests;

public sealed class TicketApiIntegrationTests
{
    [Fact]
    public async Task GetTickets_FiltersBeforePaging()
    {
        using var factory = new TicketApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        var matchingTicket = NewTicket("Matching", TicketStatus.InProgress, TicketPriority.High);
        await factory.SeedAsync(
            matchingTicket,
            NewTicket("Wrong priority", TicketStatus.InProgress, TicketPriority.Low),
            NewTicket("Wrong status", TicketStatus.New, TicketPriority.High));

        using var response = await client.GetAsync("/api/tickets?status=InProgress&priority=High&page=1&pageSize=1");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(matchingTicket.Id.ToString(), root.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetTickets_ReturnsBadRequestForInvalidPage()
    {
        using var factory = new TicketApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        using var response = await client.GetAsync("/api/tickets?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WritesAuditAndInvalidatesStatisticsCache()
    {
        using var factory = new TicketApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        var ticket = NewTicket("Status change", TicketStatus.New, TicketPriority.High);
        await factory.SeedAsync(ticket);

        using var initialStatisticsResponse = await client.GetAsync("/api/tickets/statistics");
        initialStatisticsResponse.EnsureSuccessStatusCode();
        using var initialStatistics = JsonDocument.Parse(await initialStatisticsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, FindStatusCount(initialStatistics.RootElement, "New"));

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status",
            new { newStatus = "InProgress", version = 1 });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updateBody = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, updateBody.RootElement.GetProperty("version").GetInt32());

        using var historyResponse = await client.GetAsync($"/api/tickets/{ticket.Id}/history");
        historyResponse.EnsureSuccessStatusCode();
        using var history = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        Assert.Single(history.RootElement.EnumerateArray());
        Assert.Equal("New", history.RootElement[0].GetProperty("oldStatus").GetString());
        Assert.Equal("InProgress", history.RootElement[0].GetProperty("newStatus").GetString());

        using var updatedStatisticsResponse = await client.GetAsync("/api/tickets/statistics");
        updatedStatisticsResponse.EnsureSuccessStatusCode();
        using var updatedStatistics = JsonDocument.Parse(await updatedStatisticsResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, FindStatusCount(updatedStatistics.RootElement, "New"));
        Assert.Equal(1, FindStatusCount(updatedStatistics.RootElement, "InProgress"));
    }

    [Fact]
    public async Task UpdateStatus_ReturnsConflictForStaleVersion()
    {
        using var factory = new TicketApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        var ticket = NewTicket("Concurrency", TicketStatus.New, TicketPriority.Medium);
        await factory.SeedAsync(ticket);

        using var firstResponse = await client.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status",
            new { newStatus = "InProgress", version = 1 });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var staleResponse = await client.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status",
            new { newStatus = "Waiting", version = 1 });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        using var body = JsonDocument.Parse(await staleResponse.Content.ReadAsStringAsync());
        Assert.Equal("Concurrency conflict", body.RootElement.GetProperty("title").GetString());
    }

    private static int FindStatusCount(JsonElement root, string status) =>
        root.GetProperty("byStatus")
            .EnumerateArray()
            .Single(item => item.GetProperty("status").GetString() == status)
            .GetProperty("count")
            .GetInt32();

    private static Ticket NewTicket(string title, TicketStatus status, TicketPriority priority) => new()
    {
        Title = title,
        OrganizationName = "Integration tests",
        Status = status,
        Priority = priority,
        AssignedTo = "test-agent",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Version = 1
    };
}