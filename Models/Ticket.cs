namespace HeladeriaPOS.Models;

public sealed class Ticket
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public decimal SubtotalBase { get; set; }
    public decimal TotalExtras { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal Received { get; set; }
    public decimal Change { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal CardPaid { get; set; }
    public decimal TransferPaid { get; set; }
    public string? DiscountReason { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
