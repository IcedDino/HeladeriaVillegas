using System.Text.Json;
using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public sealed class TutorialProgressService
{
    public sealed class GuideProgress
    {
        public int Step { get; set; }
        public OrderDraftService.Draft? Practice { get; set; }
        public bool Completed { get; set; }
    }
    private readonly string _guidePath = Path.Combine(FileSystem.AppDataDirectory, "Tutorials", "guided-sales.json");

    public GuideProgress LoadGuide()
    {
        try
        {
            var progress = File.Exists(_guidePath)
                ? JsonSerializer.Deserialize<GuideProgress>(File.ReadAllText(_guidePath)) : null;
            if (progress is null || progress.Step is < 0 or > 9) return new();
            if (progress.Step < 4 || progress.Practice?.Items.Count != 1) return new() { Completed = progress.Completed };
            return progress;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void SaveGuide(GuideProgress progress)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_guidePath)!);
        File.WriteAllText(_guidePath + ".tmp", JsonSerializer.Serialize(progress, JsonOptions));
        File.Move(_guidePath + ".tmp", _guidePath, overwrite: true);
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { IgnoreReadOnlyProperties = true };
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "Tutorials", "sales.json");

    public SalesTutorialSession Load()
    {
        try
        {
            var session = File.Exists(_path)
                ? JsonSerializer.Deserialize<SalesTutorialSession>(File.ReadAllText(_path)) : null;
            session ??= new SalesTutorialSession();
            session.CancelConfiguration();
            if (!Enum.IsDefined(session.Step) || (session.Step >= SalesTutorialStep.Quantity && session.Item is null))
                return new SalesTutorialSession();
            return session;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new SalesTutorialSession(); }
    }

    public void Save(SalesTutorialSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(session, JsonOptions));
        File.Move(_path + ".tmp", _path, overwrite: true);
    }
}
