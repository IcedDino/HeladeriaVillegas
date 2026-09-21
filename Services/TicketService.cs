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
}
