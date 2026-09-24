using System.Globalization;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;

namespace HeladeriaPOS.Views;

public sealed class PricesPage : ContentPage
{
    private readonly TicketService _tickets;
    private readonly MainViewModel _main;
    private readonly VerticalStackLayout _rows = new() { Spacing = 12 };
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public PricesPage(TicketService tickets, MainViewModel main)
    {
        _tickets = tickets;
        _main = main;
        Title = "Precios y disponibilidad";
        BackgroundColor = Color.FromArgb("#FFF8FA");
        var close = new Button { Text = "Cerrar", HeightRequest = 54 };
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        Content = new Grid
        {
            Padding = 16,
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { new ScrollView { Content = _rows }, close }
        };
        Grid.SetRow(close, 1);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _rows.Clear();
        _rows.Add(new Label { Text = "Sabores disponibles", FontSize = 22, FontAttributes = FontAttributes.Bold });
        var newFlavor = new Entry { Placeholder = "Nuevo sabor", WidthRequest = 240, FontSize = 18 };
        var addFlavor = new Button { Text = "Agregar sabor", HeightRequest = 52 };
        addFlavor.Clicked += async (_, _) =>
        {
            try { await _tickets.AddFlavorAsync(newFlavor.Text ?? ""); await ReloadAsync(); }
            catch (Exception ex) { await DisplayAlert("No se agregó el sabor", ex.Message, "Aceptar"); }
        };
        _rows.Add(new HorizontalStackLayout { Spacing = 10, Children = { newFlavor, addFlavor } });
        foreach (Flavor flavor in await _tickets.GetFlavorsAsync())
        {
            var toggle = new Switch { IsToggled = flavor.IsAvailable };
            toggle.Toggled += async (_, e) => await _tickets.SetFlavorAvailableAsync(flavor.Id, e.Value);
            _rows.Add(new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = flavor.Name, WidthRequest = 200, FontSize = 17, VerticalTextAlignment = TextAlignment.Center }, toggle } });
        }
        _rows.Add(new BoxView { HeightRequest = 2, Color = Color.FromArgb("#E7C6D2") });
        foreach (Product product in await _tickets.GetAllProductsAsync())
        {
            var heading = new Label { Text = $"{product.Category} · {product.Name}", FontSize = 20, FontAttributes = FontAttributes.Bold };
            _rows.Add(heading);
            var available = new Switch { IsToggled = product.IsActive };
            var availability = new HorizontalStackLayout { Spacing = 12, Children = { new Label { Text = "Disponible para vender", VerticalTextAlignment = TextAlignment.Center, FontSize = 16 }, available } };
            available.Toggled += async (_, e) =>
            {
                await _tickets.SetProductActiveAsync(product.Id, e.Value);
                await _main.RefreshProductsAsync();
            };
            _rows.Add(availability);
            if (product.ProductType == ProductType.Custom)
            {
                var baseEntry = new Entry { Text = product.BasePrice.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 110, FontSize = 18 };
                var extraEntry = new Entry { Text = product.ExtraPrice.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 110, FontSize = 18 };
                var saveCustom = new Button { Text = "Guardar precios", HeightRequest = 50 };
                saveCustom.Clicked += async (_, _) =>
                {
                    if (!decimal.TryParse(baseEntry.Text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal baseValue)
                        || !decimal.TryParse(extraEntry.Text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal extraValue))
                    { await DisplayAlert("Precio inválido", "Revisa los importes.", "Aceptar"); return; }
                    try { await _tickets.SaveCustomPriceAsync(product.Id, baseValue, extraValue); await _main.RefreshProductsAsync(); }
                    catch (Exception ex) { await DisplayAlert("No se guardó", ex.Message, "Aceptar"); }
                };
                _rows.Add(new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Base", VerticalTextAlignment = TextAlignment.Center }, baseEntry, new Label { Text = "Extra", VerticalTextAlignment = TextAlignment.Center }, extraEntry, saveCustom } });
            }
            foreach (ProductPrice price in product.Prices)
            {
                var entry = new Entry { Text = price.Amount.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 120, FontSize = 18 };
                var save = new Button { Text = "Guardar", HeightRequest = 50, WidthRequest = 110 };
                save.Clicked += async (_, _) =>
                {
                    if (!decimal.TryParse(entry.Text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) || value < 0)
                    {
                        await DisplayAlert("Precio inválido", "Escribe un importe válido.", "Aceptar");
                        return;
                    }
                    try
                    {
                        await _tickets.SavePriceAsync(price.Id, value);
                        await _main.RefreshProductsAsync();
                        await DisplayAlert("Precio guardado", $"{product.Name}: {price.Label} = {value.ToString("C", MoneyCulture)}", "Aceptar");
                    }
                    catch (Exception ex) { await DisplayAlert("No se guardó", ex.Message, "Aceptar"); }
                };
                _rows.Add(new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = price.Label, WidthRequest = 290, FontSize = 16, VerticalTextAlignment = TextAlignment.Center }, entry, save } });
            }
            _rows.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E7C6D2") });
        }
    }
}
