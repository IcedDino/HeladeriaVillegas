using System.Globalization;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace HeladeriaPOS.Views;

public sealed partial class PricesPage : ContentPage
{
    private readonly TicketService _tickets;
    private readonly MainViewModel _main;
    private readonly Action _openAddProduct;
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public PricesPage(TicketService tickets, MainViewModel main, Action openAddProduct)
    {
        _tickets = tickets;
        _main = main;
        _openAddProduct = openAddProduct;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private void OnCloseClicked(object? sender, EventArgs e) => Navigation.PopModalAsync();

    private void OnAddProductClicked(object? sender, EventArgs e)
    {
        Navigation.PopModalAsync();
        _openAddProduct();
    }

    private static Border Card()
    {
        return new Border
        {
            Padding = 14,
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#F0DDE4"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 }
        };
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        FontSize = 10.5,
        CharacterSpacing = 1.2,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#745A64")
    };

    private static Button TinyButton(string text, string background, string foreground)
    {
        return new Button
        {
            Text = text,
            MinimumHeightRequest = 44,
            CornerRadius = 11,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb(background),
            TextColor = Color.FromArgb(foreground)
        };
    }

    private async Task ReloadAsync()
    {
        Body.Clear();

        // ---- Sabores ----
        Body.Add(new Label { Text = "Sabores disponibles", FontSize = 21, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#C2185B"), Margin = new Thickness(0, 4, 0, 0) });
        Body.Add(Caption("ACTIVA O DESACTIVA LOS SABORES QUE SE OFRECEN HOY"));

        var flavorCard = Card();
        var flavorRows = new VerticalStackLayout { Spacing = 6 };
        foreach (Flavor flavor in await _tickets.GetFlavorsAsync())
        {
            var toggle = new Switch { IsToggled = flavor.IsAvailable };
            toggle.Toggled += async (_, e) => await _tickets.SetFlavorAvailableAsync(flavor.Id, e.Value);
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            row.Add(new Label { Text = flavor.Name, FontSize = 16, VerticalTextAlignment = TextAlignment.Center });
            row.Add(toggle);
            Grid.SetColumn(toggle, 1);
            flavorRows.Children.Add(row);
        }
        var newFlavor = new Entry { Placeholder = "Nombre del sabor nuevo", FontSize = 16, MinimumHeightRequest = 50 };
        var addFlavor = new Button
        {
            Text = "Agregar",
            WidthRequest = 110,
            MinimumHeightRequest = 50,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#84124B"),
            TextColor = Colors.White,
            CornerRadius = 12,
            Shadow = new Shadow { Brush = Color.FromArgb("#22000000"), Offset = new Point(0, 3), Radius = 8 }
        };
        addFlavor.Clicked += async (_, _) =>
        {
            try
            {
                await _tickets.AddFlavorAsync(newFlavor.Text ?? "");
                await ReloadAsync();
            }
            catch (Exception ex) { await DisplayAlert("No se agregó el sabor", ex.Message, "Aceptar"); }
        };
        var addRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        addRow.Add(newFlavor);
        addRow.Add(addFlavor);
        Grid.SetColumn(addFlavor, 1);
        flavorRows.Children.Insert(0, addRow);
        flavorCard.Content = flavorRows;
        Body.Add(flavorCard);

        Body.Add(new BoxView { HeightRequest = 8, Color = Color.FromArgb("#F0DDE4") });

        // ---- Productos ----
        Body.Add(new Label { Text = "Productos", FontSize = 21, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#C2185B"), Margin = new Thickness(0, 4, 0, 0) });
        Body.Add(Caption("PRECIOS, EXTRAS Y DISPONIBILIDAD DE CADA PRODUCTO"));

        foreach (Product product in await _tickets.GetAllProductsAsync())
        {
            var card = Card();
            var stack = new VerticalStackLayout { Spacing = 10 };

            var headingRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            var headings = new VerticalStackLayout { Spacing = 1 };
            headings.Children.Add(new Label { Text = product.Name, FontSize = 17, FontAttributes = FontAttributes.Bold });
            headings.Children.Add(new Label { Text = $"{product.Category} · {(product.ProductType == ProductType.Custom ? "Producto propio" : "Clásico")}{(string.IsNullOrWhiteSpace(product.Tag) ? "" : $" · {product.Tag}")}", FontSize = 12, TextColor = Color.FromArgb("#745A64") });
            headingRow.Add(headings);
            var switchHolder = new VerticalStackLayout { Spacing = 2 };
            switchHolder.Children.Add(new Label { Text = "Disponible", FontSize = 11, TextColor = Color.FromArgb("#745A64"), HorizontalTextAlignment = TextAlignment.Center });
            var available = new Switch { IsToggled = product.IsActive, Scale = 0.9 };
            available.Toggled += async (_, e) =>
            {
                await _tickets.SetProductActiveAsync(product.Id, e.Value);
                await _main.RefreshProductsAsync();
            };
            switchHolder.Children.Add(available);
            headingRow.Add(switchHolder);
            Grid.SetColumn(switchHolder, 1);
            stack.Children.Add(headingRow);

            if (product.ProductType == ProductType.Custom)
            {
                var baseEntry = new Entry { Text = product.BasePrice.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 120, FontSize = 16 };
                var extraEntry = new Entry { Text = product.ExtraPrice.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 120, FontSize = 16 };
                var saveCustom = TinyButton("Guardar precios", "#147D5B", "#FFFFFF");
                saveCustom.Clicked += async (_, _) =>
                {
                    if (!decimal.TryParse(baseEntry.Text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal baseValue)
                        || !decimal.TryParse(extraEntry.Text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal extraValue))
                    { await DisplayAlert("Precio inválido", "Revisa los importes.", "Aceptar"); return; }
                    try
                    {
                        await _tickets.SaveCustomPriceAsync(product.Id, baseValue, extraValue);
                        await _main.RefreshProductsAsync();
                        await DisplayAlert("Precio guardado", $"{product.Name}: base {baseValue.ToString("C", MoneyCulture)} · extra {extraValue.ToString("C", MoneyCulture)}", "Aceptar");
                    }
                    catch (Exception ex) { await DisplayAlert("No se guardó", ex.Message, "Aceptar"); }
                };
                var customLabel = new Label
                {
                    Text = product.AllowsExtras ? $"Venta base y extra «{product.ExtraName}»" : "Venta base",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5C6477"),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                stack.Children.Add(customLabel);
                var customRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                customRow.Add(new Label { Text = "Base", WidthRequest = 56, VerticalTextAlignment = TextAlignment.Center, FontSize = 15 });
                customRow.Add(baseEntry);
                Grid.SetColumn(baseEntry, 1);
                customRow.Add(new Label { Text = "Extra", WidthRequest = 62, VerticalTextAlignment = TextAlignment.Center, FontSize = 15 });
                customRow.Add(extraEntry);
                Grid.SetColumn(extraEntry, 3);
                customRow.Add(saveCustom);
                Grid.SetColumn(saveCustom, 4);
                stack.Children.Add(customRow);
            }

            foreach (ProductPrice price in product.Prices)
            {
                var entry = new Entry { Text = price.Amount.ToString("0.##", CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, WidthRequest = 110, FontSize = 16 };
                var save = TinyButton("Guardar", "#F2F3FF", "#3A4BC0");
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
                var priceRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                priceRow.Add(new Label { Text = price.Label, FontSize = 15, VerticalTextAlignment = TextAlignment.Center });
                priceRow.Add(entry);
                Grid.SetColumn(entry, 1);
                priceRow.Add(save);
                Grid.SetColumn(save, 2);
                stack.Children.Add(priceRow);
            }

            card.Content = stack;
            Body.Add(card);
        }

        Body.Add(new Label
        {
            Text = "Los cambios de precio y disponibilidad se aplican de inmediato en la pantalla de venta.",
            FontSize = 12,
            TextColor = Color.FromArgb("#745A64"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        });
    }
}