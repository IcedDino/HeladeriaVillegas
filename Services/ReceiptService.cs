using System.Diagnostics;
using System.Text;
using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public static class ReceiptService
{
    public static string WriteReceipt(Ticket ticket)
    {
        string folder = Path.Combine(FileSystem.AppDataDirectory, "Receipts");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ticket.OrderNumber + ".txt");
        var text = new StringBuilder();
        text.AppendLine("HELADERÍA VILLEGAS");
        text.AppendLine(ticket.OrderNumber);
        text.AppendLine(ticket.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
        text.AppendLine(new string('-', 36));
        foreach (OrderItem item in ticket.Items)
        {
            text.AppendLine($"{item.Quantity} x {item.ProductName}  {item.LineTotal:C2}");
            if (!string.IsNullOrWhiteSpace(item.SelectedVariant)) text.AppendLine($"  {item.SelectedVariant}");
            if (!string.IsNullOrWhiteSpace(item.Flavors)) text.AppendLine($"  Sabores: {item.Flavors}");
            if (!string.IsNullOrWhiteSpace(item.Instructions)) text.AppendLine($"  Indicaciones: {item.Instructions}");
            foreach (Modifier modifier in item.Modifiers) text.AppendLine($"  + {modifier.Name} x{modifier.Quantity}");
        }
        text.AppendLine(new string('-', 36));
        text.AppendLine($"Subtotal: {(ticket.SubtotalBase + ticket.TotalExtras):C2}");
        text.AppendLine($"Descuento: {ticket.Discount:C2}");
        text.AppendLine($"TOTAL: {ticket.Total:C2}");
        string paymentName = ticket.PaymentMethod switch
        {
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.Card => "Tarjeta",
            PaymentMethod.Transfer => "Transferencia",
            _ => "Mixto"
        };
        text.AppendLine($"Pago: {paymentName}");
        if (ticket.Received > 0) text.AppendLine($"Efectivo: {ticket.Received:C2} · Cambio: {ticket.Change:C2}");
        if (ticket.CardPaid > 0) text.AppendLine($"Tarjeta: {ticket.CardPaid:C2}");
        if (ticket.TransferPaid > 0) text.AppendLine($"Transferencia: {ticket.TransferPaid:C2}");
        if (ticket.Status == TicketStatus.Cancelled) text.AppendLine($"CANCELADO: {ticket.CancellationReason}");
        File.WriteAllText(path, text.ToString(), Encoding.UTF8);
        return path;
    }

    public static void PrintOnWindows(Ticket ticket)
    {
        string path = WriteReceipt(ticket);
#if WINDOWS
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print" });
#else
        throw new PlatformNotSupportedException("La impresión está disponible en Windows.");
#endif
    }
}
