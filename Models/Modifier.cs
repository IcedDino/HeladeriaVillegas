using System.ComponentModel.DataAnnotations.Schema;

namespace HeladeriaPOS.Models;

public sealed class Modifier
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public ModifierType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;

    [NotMapped]
    public decimal Total => UnitPrice * Quantity;
}
