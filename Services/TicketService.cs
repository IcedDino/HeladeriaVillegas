using HeladeriaPOS.Data;
using HeladeriaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HeladeriaPOS.Services;

public sealed class TicketService
{
    private readonly IDbContextFactory<PosDbContext> _factory;

    public TicketService(IDbContextFactory<PosDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Products
            .AsNoTracking()
            .Include(p => p.Prices)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Id)
            .ToListAsync();
    }

    public async Task SaveTicketAsync(Ticket ticket)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<Product> AddProductAsync(Product product)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public async Task UpdateProductsAsync(IEnumerable<Product> products)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Products.UpdateRange(products);
        await db.SaveChangesAsync();
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Products.AsNoTracking().Include(p => p.Prices)
            .OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
    }

    public async Task<List<Ticket>> GetTicketsAsync(DateTime day)
    {
        await using var db = await _factory.CreateDbContextAsync();
        DateTime next = day.Date.AddDays(1);
        return await db.Tickets.AsNoTracking().Include(t => t.Items).ThenInclude(i => i.Modifiers)
            .Where(t => t.CreatedAt >= day.Date && t.CreatedAt < next)
            .OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<bool> IsOrderRegisteredAsync(string orderNumber)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Tickets.AnyAsync(t => t.OrderNumber == orderNumber);
    }

    public async Task CancelTicketAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Indica el motivo de cancelación.");
        await using var db = await _factory.CreateDbContextAsync();
        Ticket ticket = await db.Tickets.SingleAsync(t => t.Id == id);
        if (ticket.Status != TicketStatus.Paid) throw new InvalidOperationException("El ticket ya no está pagado.");
        ticket.Status = TicketStatus.Cancelled;
        ticket.CancellationReason = reason.Trim();
        ticket.CancelledAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task SavePriceAsync(int priceId, decimal amount)
    {
        if (amount < 0 || amount > 100000m) throw new ArgumentOutOfRangeException(nameof(amount));
        await using var db = await _factory.CreateDbContextAsync();
        ProductPrice price = await db.ProductPrices.SingleAsync(p => p.Id == priceId);
        if (amount == 0m && price.Code is "normal" or "prepared" or "missing" or "cooked" or "Chico" or "Mediano" or "Grande" or "Jumbo" or "Doble" or "Triple" or "MedioLitro" or "UnLitro" or "CincoLitros" or "DoceLitros")
            throw new ArgumentOutOfRangeException(nameof(amount), "El precio de venta debe ser mayor que cero.");
        price.Amount = decimal.Round(amount, 2);
        await db.SaveChangesAsync();
    }

    public async Task SetProductActiveAsync(int productId, bool active)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Product product = await db.Products.SingleAsync(p => p.Id == productId);
        product.IsActive = active;
        await db.SaveChangesAsync();
    }

    public async Task<List<Flavor>> GetFlavorsAsync(bool onlyAvailable = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        IQueryable<Flavor> query = db.Flavors.AsNoTracking();
        if (onlyAvailable) query = query.Where(f => f.IsAvailable);
        return await query.OrderBy(f => f.Name).ToListAsync();
    }

    public async Task SetFlavorAvailableAsync(int id, bool available)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Flavor flavor = await db.Flavors.SingleAsync(f => f.Id == id);
        flavor.IsAvailable = available;
        await db.SaveChangesAsync();
    }

    public async Task AddFlavorAsync(string name)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 80) throw new ArgumentException("El nombre del sabor debe tener entre 2 y 80 caracteres.");
        await using var db = await _factory.CreateDbContextAsync();
        db.Flavors.Add(new Flavor { Name = name });
        await db.SaveChangesAsync();
    }

    public async Task SaveCustomPriceAsync(int productId, decimal basePrice, decimal extraPrice)
    {
        if (basePrice <= 0 || extraPrice < 0) throw new ArgumentOutOfRangeException(nameof(basePrice));
        await using var db = await _factory.CreateDbContextAsync();
        Product product = await db.Products.SingleAsync(p => p.Id == productId && p.ProductType == ProductType.Custom);
        product.BasePrice = decimal.Round(basePrice, 2);
        product.ExtraPrice = decimal.Round(extraPrice, 2);
        await db.SaveChangesAsync();
    }
}
