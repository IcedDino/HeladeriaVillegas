using System.Text.Json;
using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public sealed class OrderDraftService
{
    private readonly string _directory = Path.Combine(FileSystem.AppDataDirectory, "Drafts");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Draft
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string DiscountInput { get; set; } = "0";
        public string DiscountReason { get; set; } = string.Empty;
        public string ReceivedCashInput { get; set; } = "0";
        public string CardInput { get; set; } = "0";
        public string TransferInput { get; set; } = "0";
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public List<OrderItem> Items { get; set; } = [];
    }

    public Draft? LoadCurrent() => Load(Path.Combine(_directory, "current.json"));

    public IReadOnlyList<string> HeldOrders()
    {
        Directory.CreateDirectory(_directory);
        return Directory.GetFiles(_directory, "held_*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path)[5..]).ToList();
    }

    public Draft? LoadHeld(string name) => Load(Path.Combine(_directory, "held_" + SafeFileName(name) + ".json"));

    public void SaveCurrent(Draft draft) => Save(Path.Combine(_directory, "current.json"), draft);

    public void Hold(Draft draft)
    {
        Save(Path.Combine(_directory, "held_" + SafeFileName(draft.OrderNumber) + ".json"), draft);
        ClearCurrent();
    }

    public void DeleteHeld(string name) => File.Delete(Path.Combine(_directory, "held_" + SafeFileName(name) + ".json"));
    public void ClearCurrent() => File.Delete(Path.Combine(_directory, "current.json"));

    private static string SafeFileName(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private static Draft? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Draft>(File.ReadAllText(path), JsonOptions);
    }

    private static void Save(string path, Draft draft)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(draft, JsonOptions));
        File.Move(temporary, path, true);
    }
}
