using System.Globalization;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;

namespace HeladeriaPOS.Views;

public sealed class SalesTicketRow
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public SalesTicketRow(Ticket ticket) => Ticket = ticket;

    public Ticket Ticket { get; }

    public string TimeDisplay => Ticket.CreatedAt.ToString("HH:mm");
    public string OrderNumber => Ticket.OrderNumber;
    public string TotalDisplay => Ticket.Total.ToString("C0", MoneyCulture);
    public string StatusText => Ticket.Status == TicketStatus.Paid ? "PAGADA" : "CANCELADA";
    public Color StatusColor => Ticket.Status == TicketStatus.Paid ? Color.FromArgb("#147D5B") : Color.FromArgb("#B3261E");
    public bool CanCancel => Ticket.Status == TicketStatus.Paid;

    public string PaymentSummary
    {
        get
        {
            string pay = Ticket.PaymentMethod switch
            {
                PaymentMethod.Cash => "Efectivo",
                PaymentMethod.Card => "Tarjeta",
                PaymentMethod.Transfer => "Transferencia",
                _ => "Mixto"
            };
            decimal card = Ticket.CardPaid;
            decimal transfer = Ticket.TransferPaid;
            if (card > 0 || transfer > 0)
                pay += $" · {card.ToString("C0", MoneyCulture)} tarjeta + {transfer.ToString("C0", MoneyCulture)} transferencia";
            if (Ticket.Status == TicketStatus.Paid && Ticket.Change > 0)
                pay += $" · cambio " + Ticket.Change.ToString("C0", MoneyCulture);
            if (Ticket.Status == TicketStatus.Paid && Ticket.Discount > 0)
                pay += $" · descuento " + Ticket.Discount.ToString("C0", MoneyCulture);
            return pay;
        }
    }

    public string ItemsSummary
    {
        get
        {
            var lines = new List<string>(Ticket.Items.Count);
            foreach (OrderItem item in Ticket.Items)
            {
                string detail = $"{item.Quantity} × {item.ProductName}";
                if (!string.IsNullOrWhiteSpace(item.SelectedVariant)) detail += $" · {item.SelectedVariant}";
                if (!string.IsNullOrWhiteSpace(item.Flavors)) detail += $" · {item.Flavors}";
                if (!string.IsNullOrWhiteSpace(item.Instructions)) detail += $" · {item.Instructions}";
                lines.Add(detail);
            }
            return string.Join("\n", lines);
        }
    }
}

public sealed partial class SalesPage : ContentPage
{
    private readonly TicketService _tickets;
    private readonly List<SalesTicketRow> _rows = [];
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public SalesPage(TicketService tickets)
    {
        _tickets = tickets;
        InitializeComponent();
        SalesDatePicker.Date = DateTime.Today;
        SalesDatePicker.DateSelected += async (_, _) => await ReloadAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private void OnCloseClicked(object? sender, EventArgs e) => Navigation.PopModalAsync();

    private async void OnTodayClicked(object? sender, EventArgs e)
    {
        SalesDatePicker.Date = DateTime.Today;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        DateTime day = SalesDatePicker.Date;
        List<Ticket> tickets = await _tickets.GetTicketsAsync(day);
        List<Ticket> paid = tickets.Where(t => t.Status == TicketStatus.Paid).ToList();

        TotalDayLabel.Text = paid.Sum(t => t.Total).ToString("C0", MoneyCulture);
        CountLabel.Text = paid.Count == 1 ? "1 venta" : $"{paid.Count} ventas";
        CashLabel.Text = paid.Sum(t => t.Total - t.CardPaid - t.TransferPaid).ToString("C0", MoneyCulture);
        CardLabel.Text = paid.Sum(t => t.CardPaid + t.TransferPaid).ToString("C0", MoneyCulture);

        _rows.Clear();
        foreach (Ticket ticket in tickets)
            _rows.Add(new SalesTicketRow(ticket));
        TicketsList.ItemsSource = _rows;
    }

    private async void OnPrintClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not SalesTicketRow row) return;
        try
        {
            ReceiptService.PrintOnWindows(row.Ticket);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Impresión no disponible", $"Se guardó el comprobante en\n{ReceiptService.WriteReceipt(row.Ticket)}\n\n{ex.Message}", "Aceptar");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not SalesTicketRow row) return;
        string? reason = await DisplayPromptAsync("Cancelar venta", "Motivo de cancelación", "Continuar", "Volver", maxLength: 200);
        if (string.IsNullOrWhiteSpace(reason)) return;
        if (!await DisplayAlert("Confirmar cancelación", $"¿Cancelar {row.OrderNumber} por importe de {row.TotalDisplay}?", "Cancelar venta", "Volver")) return;
        await _tickets.CancelTicketAsync(row.Ticket.Id, reason);
        await ReloadAsync();
    }
}