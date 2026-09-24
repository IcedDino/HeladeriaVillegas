namespace HeladeriaPOS.Models;

public sealed class ProductPrice
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
