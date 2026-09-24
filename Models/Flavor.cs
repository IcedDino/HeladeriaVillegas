namespace HeladeriaPOS.Models;

public sealed class Flavor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
}
