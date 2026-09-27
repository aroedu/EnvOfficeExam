using Microsoft.EntityFrameworkCore;
using TicketManagement.Api.Data;
using TicketManagement.Api.Models;

namespace TicketManagement.Api.IntegrationTests;

public sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Ticket>().Property(ticket => ticket.CreatedAt).HasConversion<long>();
        modelBuilder.Entity<Ticket>().Property(ticket => ticket.UpdatedAt).HasConversion<long>();
        modelBuilder.Entity<TicketAuditLog>().Property(log => log.ChangedAt).HasConversion<long>();
    }
}