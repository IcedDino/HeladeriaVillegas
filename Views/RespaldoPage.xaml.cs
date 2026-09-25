using HeladeriaPOS.Services;

namespace HeladeriaPOS.Views;

public sealed partial class RespaldoPage : ContentPage
{
    private readonly BackupService _backup;

    public RespaldoPage(BackupService backup)
    {
        _backup = backup;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateBackupInfo();
    }

    private void OnCloseClicked(object? sender, EventArgs e) => Navigation.PopModalAsync();

    private void UpdateBackupInfo()
    {
        AutoBackupLabel.Text = _backup.HasTodayBackup
            ? "La copia automática de HOY ya está creada."
            : "Hoy aún no hay copia automática. Al abrir la app se intenta crear una.";

        string? latest = _backup.LatestBackupPath;
        if (latest is null)
        {
            LastBackupLabel.Text = "Aún no hay respaldos.";
            BackupPathLabel.Text = "La carpeta de respaldos está vacía.";
            return;
        }

        DateTime created = File.GetLastWriteTime(latest);
        LastBackupLabel.Text = created.ToString("dd MMMM yyyy 'a las' HH:mm");
        BackupPathLabel.Text = latest;
    }

    private async void OnCreateBackupClicked(object? sender, EventArgs e)
    {
        CreateResultLabel.IsVisible = false;
        try
        {
            string path = _backup.CreateBackup();
            UpdateBackupInfo();
            CreateResultLabel.Text = $"✓ Respaldo verificado:\n{path}";
            CreateResultLabel.TextColor = Color.FromArgb("#147D5B");
            CreateResultLabel.IsVisible = true;
            await DisplayAlert("Respaldo verificado", $"Copia creada en:\n{path}\n\nCópiala también a una USB o nube para protegerla si falla esta computadora.", "Aceptar");
        }
        catch (Exception ex)
        {
            CreateResultLabel.Text = $"✗ No se pudo crear: {ex.Message}";
            CreateResultLabel.TextColor = Color.FromArgb("#93000A");
            CreateResultLabel.IsVisible = true;
            await DisplayAlert("Error de respaldo", ex.Message, "Aceptar");
        }
    }

    private async void OnRestoreBackupClicked(object? sender, EventArgs e)
    {
        RestoreResultLabel.IsVisible = false;
        try
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecciona una copia pos_*.db" });
            if (file is null)
                return;
            if (!await DisplayAlert("Restaurar ventas", "Al reiniciar, el respaldo reemplazará la base actual. Se conservará una copia de la base anterior. ¿Continuar?", "Preparar restauración", "Volver"))
                return;
            _backup.ScheduleRestore(file.FullPath);
            RestoreResultLabel.Text = "✓ Restauración preparada. Cierra y vuelve a abrir la aplicación.";
            RestoreResultLabel.TextColor = Color.FromArgb("#147D5B");
            RestoreResultLabel.IsVisible = true;
            await DisplayAlert("Restauración preparada", "Cierra y vuelve a abrir la aplicación para aplicar el respaldo.", "Aceptar");
        }
        catch (Exception ex)
        {
            RestoreResultLabel.Text = $"✗ No se pudo restaurar: {ex.Message}";
            RestoreResultLabel.TextColor = Color.FromArgb("#93000A");
            RestoreResultLabel.IsVisible = true;
            await DisplayAlert("Error de respaldo", ex.Message, "Aceptar");
        }
    }
}