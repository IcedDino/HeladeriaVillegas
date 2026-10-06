using System.Text.Json;
using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public static class OrderDisplayNames
{
    private static readonly object Gate = new();
    private static Dictionary<string, int>? _numbers;

    public static void Initialize(IEnumerable<string> actualOrders)
    {
        lock (Gate)
        {
            _ = For("__initialize__");
            var updated = new Dictionary<string, int>();
            foreach (string order in actualOrders.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()
                .OrderBy(n => _numbers!.TryGetValue(n, out int number) ? number : int.MaxValue))
                updated[order] = updated.Count + 1;
            Persist(updated);
        }
    }

    public static void Reserve(string orderNumber)
    {
        lock (Gate)
        {
            _ = For(orderNumber);
            if (_numbers!.ContainsKey(orderNumber)) return;
            var updated = new Dictionary<string, int>(_numbers)
            {
                [orderNumber] = _numbers.Values.DefaultIfEmpty(0).Max() + 1
            };
            Persist(updated);
        }
    }

    public static void Forget(string orderNumber)
    {
        lock (Gate)
        {
            _ = For(orderNumber);
            if (!_numbers!.ContainsKey(orderNumber)) return;
            var updated = new Dictionary<string, int>(_numbers);
            updated.Remove(orderNumber);
            Persist(updated);
        }
    }

    private static void Persist(Dictionary<string, int> updated)
    {
        string path = Path.Combine(FileSystem.AppDataDirectory, "order-display-numbers.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(updated));
        File.Move(path + ".tmp", path, true);
        _numbers = updated;
    }

    public static string For(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return "Orden";
        lock (Gate)
        {
            string path = Path.Combine(FileSystem.AppDataDirectory, "order-display-numbers.json");
            _numbers ??= File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? new()
                : new();
            if (!_numbers.TryGetValue(orderNumber, out int number))
                number = _numbers.Values.DefaultIfEmpty(0).Max() + 1;
            return $"Orden {number}";
        }
    }
}

public sealed class OrderDraftService
{
    private readonly string _directory = Path.Combine(FileSystem.AppDataDirectory, "Drafts");
    private static readonly JsonSerializerOptions JsonOptions = new() { IgnoreReadOnlyProperties = true };
    private string? _lastCurrentJson;

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

    public void SaveCurrent(Draft draft)
    {
        string path = Path.Combine(_directory, "current.json");
        string json = JsonSerializer.Serialize(draft, JsonOptions);
        if (json == _lastCurrentJson && File.Exists(path)) return;
        SaveJson(path, json);
        _lastCurrentJson = json;
    }

    public void Hold(Draft draft)
    {
        Save(Path.Combine(_directory, "held_" + SafeFileName(draft.OrderNumber) + ".json"), draft);
        ClearCurrent();
    }

    public void DeleteHeld(string name) => File.Delete(Path.Combine(_directory, "held_" + SafeFileName(name) + ".json"));
    public void ClearCurrent()
    {
        File.Delete(Path.Combine(_directory, "current.json"));
        _lastCurrentJson = null;
    }

    private static string SafeFileName(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private static Draft? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Draft>(File.ReadAllText(path), JsonOptions);
    }

    private static void Save(string path, Draft draft)
        => SaveJson(path, JsonSerializer.Serialize(draft, JsonOptions));

    private static void SaveJson(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, true);
    }
}
