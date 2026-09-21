namespace HeladeriaPOS.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public ProductType ProductType { get; set; }
    public decimal BasePrice { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageCreator { get; set; }
    public string? ImageLicense { get; set; }
    public string? ImageLicenseUrl { get; set; }
    public string? ImageSourceUrl { get; set; }
    public string? Tag { get; set; }
    public bool AllowsExtras { get; set; }
    public string? ExtraName { get; set; }
    public decimal ExtraPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
