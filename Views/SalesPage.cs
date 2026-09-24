using System.Globalization;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;

namespace HeladeriaPOS.Views;

public sealed class SalesPage : ContentPage
{
    private readonly TicketService _tickets;
    private readonly DatePicker _date = new() { Date = DateTime.Today };
    private readonly VerticalStackLayout _rows = new() { Spacing = 10 };
    private readonly Label _summary = new() { FontSize = 18, FontAttributes = FontAttributes.Bold };
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public SalesPage(TicketService tickets)
    {
        _tickets = tickets;
        Title = "Ventas y corte";
        BackgroundColor = Color.FromArgb("#FFF8FA");
        var close = new Button { Text = "Cerrar", HeightRequest = 54 };
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var header = new VerticalStackLayout { Spacing = 10, Children = { new Label { Text = "Ventas del día", FontSize = 26, FontAttributes = FontAttributes.Bold }, _date, _summary } };
        _date.DateSelected += async (_, _) => await ReloadAsync();
        var grid = new Grid { Padding = 16, RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star }, new RowDefinition { Height = GridLength.Auto } } };
        grid.Add(header);
        var scroll = new ScrollView { Content = _rows };
        grid.Add(scroll);
        Grid.SetRow(scroll, 1);
        grid.Add(close);
        Grid.SetRow(close, 2);
        Content = grid;
    }

    protected override async void OnAppearing() { base.OnAppearing(); await ReloadAsync(); }

    private async Task ReloadAsync()
    {
        List<Ticket> tickets = await _tickets.GetTicketsAsync(_date.Date);
        List<Ticket> paid = tickets.Where(t => t.Status == TicketStatus.Paid).ToList();
        _summary.Text = $"{paid.Count} ventas · {paid.Sum(t => t.Total).ToString("C", MoneyCulture)}\nEfectivo: {paid.Sum(t => t.Total - t.CardPaid - t.TransferPaid).ToString("C", MoneyCulture)} · Tarjeta: {paid.Sum(t => t.CardPaid).ToString("C", MoneyCulture)} · Transferencia: {paid.Sum(t => t.TransferPaid).ToString("C", MoneyCulture)}";
        _rows.Clear();
        foreach (Ticket ticket in tickets)
        {
            var title = new Label { Text = $"{ticket.CreatedAt:HH:mm} · {ticket.OrderNumber} · {ticket.Total.ToString("C", MoneyCulture)} · {ticket.Status}", FontSize = 18, FontAttributes = FontAttributes.Bold };
            var detail = new Label { Text = string.Join("\n", ticket.Items.Select(i => $"{i.Quantity} × {i.ProductName} {i.SelectedVariant} {i.Flavors} {i.Instructions}")), FontSize = 14 };
            var buttons = new HorizontalStackLayout { Spacing = 10 };
            var print = new Button { Text = "Imprimir", HeightRequest = 50 };
            print.Clicked += async (_, _) =>
            {
                try { ReceiptService.PrintOnWindows(ticket); }
                catch (Exception ex) { await DisplayAlert("Impresión no disponible", $"Se guardó el comprobante en {ReceiptService.WriteReceipt(ticket)}. {ex.Message}", "Aceptar"); }
            };
            buttons.Add(print);
            if (ticket.Status == TicketStatus.Paid)
            {
                var cancel = new Button { Text = "Cancelar venta", BackgroundColor = Color.FromArgb("#FFDAD6"), HeightRequest = 50 };
                cancel.Clicked += async (_, _) =>
                {
                    string? reason = await DisplayPromptAsync("Cancelar venta", "Motivo de cancelación", "Continuar", "Volver", maxLength: 200);
                    if (string.IsNullOrWhiteSpace(reason)) return;
                    if (!await DisplayAlert("Confirmar cancelación", $"¿Cancelar {ticket.OrderNumber}?", "Cancelar venta", "Volver")) return;
                    await _tickets.CancelTicketAsync(ticket.Id, reason);
                    await ReloadAsync();
                };
                buttons.Add(cancel);
            }
            _rows.Add(new Border { Stroke = Color.FromArgb("#E7C6D2"), BackgroundColor = Colors.White, Padding = 12,
                Content = new VerticalStackLayout { Spacing = 8, Children = { title, detail, buttons } } });
        }
    }
}
