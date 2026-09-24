using Microsoft.EntityFrameworkCore;
using TicketManagement.Api.Models;

namespace TicketManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketAuditLog> TicketAuditLogs => Set<TicketAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.OrganizationName).IsRequired().HasMaxLength(200);
            entity.Property(t => t.AssignedTo).HasMaxLength(200);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Version).IsConcurrencyToken();

            // Indexes supporting the filter/sort/search scenarios in section 1.
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.AssignedTo);
            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => new { t.Status, t.Priority, t.CreatedAt });

            entity.HasMany(t => t.AuditLogs)
                .WithOne(a => a.Ticket)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketAuditLog>(entity =>
        {
            entity.ToTable("ticket_audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.OldStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.NewStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(a => new { a.TicketId, a.ChangedAt });
        });
    }
}
