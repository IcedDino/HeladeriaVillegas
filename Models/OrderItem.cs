using System.ComponentModel.DataAnnotations.Schema;

namespace HeladeriaPOS.Models;

public sealed class OrderItem
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal BaseUnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string? SelectedVariant { get; set; }
    public List<Modifier> Modifiers { get; set; } = [];

    [NotMapped]
    public decimal ExtrasUnitTotal
    {
        get
        {
            decimal total = 0m;
            for (int i = 0; i < Modifiers.Count; i++)
                total += Modifiers[i].UnitPrice * Modifiers[i].Quantity;
            return total;
        }
    }

    [NotMapped]
    public decimal UnitTotal => BaseUnitPrice + ExtrasUnitTotal;

    [NotMapped]
    public decimal LineTotal => UnitTotal * Quantity;
}
