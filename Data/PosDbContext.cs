using HeladeriaPOS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HeladeriaPOS.Data;

public sealed class PosDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var moneyConverter = new ValueConverter<decimal, long>(
            value => decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero)),
            value => value / 100m);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Tag).HasMaxLength(50);
            entity.Property(x => x.ImagePath).HasMaxLength(260);
            entity.Property(x => x.ImageCreator).HasMaxLength(300);
            entity.Property(x => x.ImageLicense).HasMaxLength(80);
            entity.Property(x => x.ImageLicenseUrl).HasMaxLength(2000);
            entity.Property(x => x.ImageSourceUrl).HasMaxLength(2000);
            entity.Property(x => x.ExtraName).HasMaxLength(80);
            entity.Property(x => x.BasePrice).HasConversion(moneyConverter);
            entity.Property(x => x.ExtraPrice).HasConversion(moneyConverter);
            entity.HasIndex(x => new { x.Category, x.IsActive });
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.SubtotalBase).HasConversion(moneyConverter);
            entity.Property(x => x.TotalExtras).HasConversion(moneyConverter);
            entity.Property(x => x.Discount).HasConversion(moneyConverter);
            entity.Property(x => x.Total).HasConversion(moneyConverter);
            entity.Property(x => x.Received).HasConversion(moneyConverter);
            entity.Property(x => x.Change).HasConversion(moneyConverter);
            entity.HasMany(x => x.Items)
                  .WithOne()
                  .HasForeignKey(x => x.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SelectedVariant).HasMaxLength(180);
            entity.Property(x => x.BaseUnitPrice).HasConversion(moneyConverter);
            entity.HasMany(x => x.Modifiers)
                  .WithOne()
                  .HasForeignKey(x => x.OrderItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Modifier>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitPrice).HasConversion(moneyConverter);
        });
    }
}
