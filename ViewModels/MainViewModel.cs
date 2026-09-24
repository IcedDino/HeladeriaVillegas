using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeladeriaPOS.Data;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;

namespace HeladeriaPOS.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("es-MX");
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly PricingService _pricingService;
    private readonly IProductDialogService _dialogService;
    private readonly TicketService _ticketService;
    private readonly OrderDraftService _draftService;
    private readonly BackupService _backupService;
    private bool _loaded;
    private bool _restoring;
    private OrderDraftService.Draft? _lastCleared;

    public ObservableCollection<ProductCardViewModel> Products { get; } = [];
    public ObservableCollection<ProductCardViewModel> FilteredProducts { get; } = [];
    public ObservableCollection<OrderItemViewModel> Cart { get; } = [];

    [ObservableProperty]
    private double productCardHeight = 240;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSnacksSelected))]
    [NotifyPropertyChangedFor(nameof(IsHeladosSelected))]
    [NotifyPropertyChangedFor(nameof(IsEspecialidadesSelected))]
    private ProductCategory selectedCategory = ProductCategory.Helados;

    [ObservableProperty]
    private string orderNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtotalBaseDisplay))]
    private decimal subtotalBase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalExtrasDisplay))]
    private decimal totalExtras;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountDisplay))]
    private decimal discountApplied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalDisplay))]
    [NotifyPropertyChangedFor(nameof(CheckoutLabel))]
    private decimal total;

    [ObservableProperty]
    private string discountInput = "0";

    [ObservableProperty]
    private string receivedCashInput = "0";

    [ObservableProperty]
    private string cardInput = "0";

    [ObservableProperty]
    private string transferInput = "0";

    [ObservableProperty]
    private string discountReason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCashPayment))]
    [NotifyPropertyChangedFor(nameof(IsMixedPayment))]
    [NotifyPropertyChangedFor(nameof(PaymentMethodDisplay))]
    private PaymentMethod paymentMethod = HeladeriaPOS.Models.PaymentMethod.Cash;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeDisplay))]
    private decimal change;

    [ObservableProperty]
    private string statusMessage = "Listo para vender";

    [ObservableProperty]
    private bool isBusy;

    public bool IsSnacksSelected => SelectedCategory == ProductCategory.Snacks;
    public bool IsHeladosSelected => SelectedCategory == ProductCategory.Helados;
    public bool IsEspecialidadesSelected => SelectedCategory == ProductCategory.Especialidades;
    public bool HasItemsInCart => Cart.Count > 0;
    public bool IsCashPayment => PaymentMethod is HeladeriaPOS.Models.PaymentMethod.Cash or HeladeriaPOS.Models.PaymentMethod.Mixed;
    public bool IsMixedPayment => PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Mixed;
    public bool CanUndoClear => _lastCleared is not null;
    public string PaymentMethodDisplay => PaymentMethod switch
    {
        HeladeriaPOS.Models.PaymentMethod.Cash => "Pago: efectivo",
        HeladeriaPOS.Models.PaymentMethod.Card => "Pago: tarjeta",
        HeladeriaPOS.Models.PaymentMethod.Transfer => "Pago: transferencia",
        _ => "Pago: mixto"
    };

    public string SubtotalBaseDisplay => SubtotalBase.ToString("C0", CurrencyCulture);
    public string TotalExtrasDisplay => TotalExtras.ToString("C0", CurrencyCulture);
    public string DiscountDisplay => DiscountApplied.ToString("C0", CurrencyCulture);
    public string TotalDisplay => Total.ToString("C0", CurrencyCulture);
    public string ChangeDisplay => Change.ToString("C0", CurrencyCulture);
    public string CheckoutLabel => $"Cobrar & Registrar  ·  {TotalDisplay}";
    public string CheckoutHint
    {
        get
        {
            if (Cart.Count == 0) return "Agrega productos para empezar.";
            if (DiscountApplied >= SubtotalBase + TotalExtras) return "El descuento debe ser menor que el subtotal.";
            if (DiscountApplied > 0 && string.IsNullOrWhiteSpace(DiscountReason)) return "Indica el motivo del descuento.";
            if (IsMixedPayment && ParseMoney(CardInput) + ParseMoney(TransferInput) > Total) return "Tarjeta y transferencia superan el total.";
            if (IsCashPayment && ParseMoney(ReceivedCashInput) + (IsMixedPayment ? ParseMoney(CardInput) + ParseMoney(TransferInput) : 0m) < Total)
                return "Falta registrar el efectivo recibido.";
            return "Listo para cobrar.";
        }
    }
    public string CartItemCountDisplay
    {
        get
        {
            int count = Cart.Sum(item => item.Quantity);
            return $"{count} {(count == 1 ? "artículo" : "artículos")}";
        }
    }

    public MainViewModel(
        DatabaseInitializer databaseInitializer,
        PricingService pricingService,
        IProductDialogService dialogService,
        TicketService ticketService,
        OrderDraftService draftService,
        BackupService backupService)
    {
        _databaseInitializer = databaseInitializer;
        _pricingService = pricingService;
        _dialogService = dialogService;
        _ticketService = ticketService;
        _draftService = draftService;
        _backupService = backupService;
        CreateNewOrder();
    }

    public async Task LoadAsync()
    {
        if (_loaded)
            return;

        IsBusy = true;
        StatusMessage = "Preparando base de datos...";

        try
        {
            await _databaseInitializer.InitializeAsync();
            List<Product> products = await _ticketService.GetProductsAsync();

            Products.Clear();
            for (int i = 0; i < products.Count; i++)
                Products.Add(new ProductCardViewModel(products[i], SelectProductAsync));

            ApplyCategoryFilter();
            OrderDraftService.Draft? draft = _draftService.LoadCurrent();
            if (draft is not null && await _ticketService.IsOrderRegisteredAsync(draft.OrderNumber))
            {
                _draftService.ClearCurrent();
                draft = null;
            }
            if (draft is not null) RestoreDraft(draft);
            _loaded = true;
            StatusMessage = draft is null ? "Listo para vender" : $"Orden {OrderNumber} recuperada";
            try { _backupService.BackupOncePerDay(); }
            catch (Exception ex) { StatusMessage += $" · Respaldo pendiente: {ex.Message}"; }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al iniciar: {ex.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddProductAsync(
        ProductCategory category,
        string name,
        decimal basePrice,
        string? tag,
        bool allowsExtras,
        string? extraName,
        decimal extraPrice,
        OpenverseImageResult? image)
    {
        var product = new Product
        {
            Name = name.Trim(),
            Category = category,
            ProductType = ProductType.Custom,
            BasePrice = basePrice,
            Tag = string.IsNullOrWhiteSpace(tag) ? "Personalizado" : tag.Trim(),
            ImagePath = image?.Thumbnail ?? "placeholder.png",
            ImageCreator = image?.Creator,
            ImageLicense = image is null ? null : $"{image.License.ToUpperInvariant()} {image.LicenseVersion}".Trim(),
            ImageLicenseUrl = image?.LicenseUrl,
            ImageSourceUrl = image?.SourceUrl,
            AllowsExtras = allowsExtras,
            ExtraName = allowsExtras
                ? (string.IsNullOrWhiteSpace(extraName) ? "Extra" : extraName.Trim())
                : null,
            ExtraPrice = allowsExtras ? extraPrice : 0m,
            IsActive = true
        };

        await _ticketService.AddProductAsync(product);

        var card = new ProductCardViewModel(product, SelectProductAsync);
        Products.Add(card);
        if (product.Category == SelectedCategory)
            FilteredProducts.Add(card);

        StatusMessage = $"Producto {product.Name} agregado";
    }

    [RelayCommand]
    private void SelectCategory(string categoryName)
    {
        if (!Enum.TryParse<ProductCategory>(categoryName, true, out ProductCategory category))
            return;

        if (SelectedCategory == category)
            return;

        SelectedCategory = category;
        ApplyCategoryFilter();
    }

    private void ApplyCategoryFilter()
    {
        FilteredProducts.Clear();
        for (int i = 0; i < Products.Count; i++)
        {
            ProductCardViewModel product = Products[i];
            if (product.Model.Category == SelectedCategory)
                FilteredProducts.Add(product);
        }
    }

    private async Task SelectProductAsync(ProductCardViewModel productCard)
    {
        if (IsBusy)
            return;

        ProductSelection? selection = await _dialogService.ConfigureAsync(productCard.Model);
        if (selection is null)
            return;

        PricingResult pricing = _pricingService.Calculate(productCard.Model, selection);

        var model = new OrderItem
        {
            ProductId = productCard.Model.Id,
            ProductName = productCard.Model.Name,
            BaseUnitPrice = pricing.BasePrice,
            SelectedVariant = pricing.VariantDescription,
            Quantity = 1,
            Modifiers = pricing.Modifiers
            ,Flavors = selection.Flavors,
            Instructions = selection.Instructions
        };

        Cart.Add(new OrderItemViewModel(model, RemoveItem, CalculateTotal));
        NotifyCartStateChanged();
        CalculateTotal();
        StatusMessage = $"{productCard.Name} agregado al ticket";
        SaveDraft();
    }

    private void RemoveItem(OrderItemViewModel item)
    {
        Cart.Remove(item);
        NotifyCartStateChanged();
        CalculateTotal();
        StatusMessage = "Partida eliminada";
        SaveDraft();
    }

    partial void OnDiscountInputChanged(string value)
    {
        DiscountApplied = ParseMoney(value);
        CalculateTotal();
        SaveDraft();
    }

    partial void OnReceivedCashInputChanged(string value)
    {
        CalculateChange();
        CheckoutCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CheckoutHint));
        SaveDraft();
    }

    public async Task RefreshProductsAsync()
    {
        List<Product> products = await _ticketService.GetProductsAsync();
        Products.Clear();
        foreach (Product product in products)
            Products.Add(new ProductCardViewModel(product, SelectProductAsync));
        ApplyCategoryFilter();
    }

    partial void OnCardInputChanged(string value) { CalculateChange(); CheckoutCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CheckoutHint)); SaveDraft(); }
    partial void OnTransferInputChanged(string value) { CalculateChange(); CheckoutCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CheckoutHint)); SaveDraft(); }
    partial void OnDiscountReasonChanged(string value) { CheckoutCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CheckoutHint)); SaveDraft(); }
    partial void OnPaymentMethodChanged(PaymentMethod value) { CalculateChange(); CheckoutCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CheckoutHint)); SaveDraft(); }

    [RelayCommand]
    private void SelectPayment(string method)
    {
        if (Enum.TryParse(method, out PaymentMethod selected)) PaymentMethod = selected;
    }

    public void CalculateTotal()
    {
        OnPropertyChanged(nameof(CartItemCountDisplay));
        decimal baseSubtotal = 0m;
        decimal extrasSubtotal = 0m;

        for (int i = 0; i < Cart.Count; i++)
        {
            OrderItemViewModel item = Cart[i];
            int quantity = item.Quantity;
            baseSubtotal += item.BaseUnitPrice * quantity;
            extrasSubtotal += item.ExtrasUnitTotal * quantity;
        }

        SubtotalBase = baseSubtotal;
        TotalExtras = extrasSubtotal;
        decimal beforeDiscount = baseSubtotal + extrasSubtotal;
        Total = Math.Max(0m, beforeDiscount - Math.Min(DiscountApplied, beforeDiscount));

        CalculateChange();
        CheckoutCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CheckoutHint));
        SaveDraft();
    }

    private void CalculateChange()
    {
        decimal received = ParseMoney(ReceivedCashInput);
        decimal dueFromCash = Math.Max(0m, Total - (PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Mixed ? ParseMoney(CardInput) + ParseMoney(TransferInput) : 0m));
        Change = IsCashPayment && received > dueFromCash ? received - dueFromCash : 0m;
    }

    private bool CanCheckout()
    {
        decimal received = ParseMoney(ReceivedCashInput);
        decimal other = PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Mixed ? ParseMoney(CardInput) + ParseMoney(TransferInput) : 0m;
        bool paid = PaymentMethod switch
        {
            HeladeriaPOS.Models.PaymentMethod.Cash => received >= Total,
            HeladeriaPOS.Models.PaymentMethod.Card or HeladeriaPOS.Models.PaymentMethod.Transfer => true,
            HeladeriaPOS.Models.PaymentMethod.Mixed => other <= Total && received + other >= Total,
            _ => false
        };
        return !IsBusy && Cart.Count > 0 && Total > 0m && DiscountApplied < SubtotalBase + TotalExtras
            && (DiscountApplied == 0m || !string.IsNullOrWhiteSpace(DiscountReason)) && paid;
    }

    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private async Task CheckoutAsync()
    {
        if (!CanCheckout())
            return;

        IsBusy = true;
        CheckoutCommand.NotifyCanExecuteChanged();
        StatusMessage = "Registrando venta...";

        try
        {
            var ticket = new Ticket
            {
                OrderNumber = OrderNumber,
                CreatedAt = DateTime.Now,
                PaidAt = DateTime.Now,
                Status = TicketStatus.Paid,
                SubtotalBase = SubtotalBase,
                TotalExtras = TotalExtras,
                Discount = Math.Min(DiscountApplied, SubtotalBase + TotalExtras),
                DiscountReason = DiscountReason.Trim(),
                Total = Total,
                PaymentMethod = PaymentMethod,
                Received = IsCashPayment ? ParseMoney(ReceivedCashInput) : 0m,
                CardPaid = PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Card ? Total : PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Mixed ? ParseMoney(CardInput) : 0m,
                TransferPaid = PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Transfer ? Total : PaymentMethod == HeladeriaPOS.Models.PaymentMethod.Mixed ? ParseMoney(TransferInput) : 0m,
                Change = Change,
                Items = new List<OrderItem>(Cart.Count)
            };

            for (int i = 0; i < Cart.Count; i++)
                ticket.Items.Add(Cart[i].Model);

            await _ticketService.SaveTicketAsync(ticket);
            _draftService.ClearCurrent();
            string paidOrder = OrderNumber;
            CreateNewOrder();
            StatusMessage = $"Venta {paidOrder} registrada correctamente";
        }
        catch (Exception ex)
        {
            bool alreadyRegistered = false;
            try { alreadyRegistered = await _ticketService.IsOrderRegisteredAsync(OrderNumber); }
            catch { /* Se conserva la orden para volver a intentar cuando la base esté disponible. */ }
            if (alreadyRegistered)
            {
                _draftService.ClearCurrent();
                CreateNewOrder();
                StatusMessage = "La venta ya estaba registrada. Revisa el historial.";
            }
            else
            {
                StatusMessage = $"No se registró la venta: {ex.Message}";
                SaveDraft();
            }
        }
        finally
        {
            IsBusy = false;
            CheckoutCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ApplyQuickCash(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return;

        if (string.Equals(parameter, "Exacto", StringComparison.OrdinalIgnoreCase))
        {
            ReceivedCashInput = Total.ToString("0.##", CultureInfo.InvariantCulture);
            return;
        }

        if (string.Equals(parameter, "Limpiar", StringComparison.OrdinalIgnoreCase))
        {
            ReceivedCashInput = "0";
            return;
        }

        if (decimal.TryParse(parameter, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
        {
            decimal current = ParseMoney(ReceivedCashInput);
            ReceivedCashInput = (current + amount).ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    [RelayCommand]
    private void ClearCart()
    {
        if (Cart.Count == 0)
            return;

        Cart.Clear();
        NotifyCartStateChanged();
        CalculateTotal();
        StatusMessage = "Ticket vaciado";
        SaveDraft();
    }

    [RelayCommand]
    private void NewTicket()
    {
        CreateNewOrder();
        StatusMessage = "Nuevo ticket";
    }

    private void CreateNewOrder()
    {
        Cart.Clear();
        SubtotalBase = 0m;
        TotalExtras = 0m;
        DiscountApplied = 0m;
        DiscountInput = "0";
        Total = 0m;
        ReceivedCashInput = "0";
        CardInput = "0";
        TransferInput = "0";
        DiscountReason = string.Empty;
        PaymentMethod = HeladeriaPOS.Models.PaymentMethod.Cash;
        Change = 0m;
        OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd-HHmmssfff}";
        NotifyCartStateChanged();
        CheckoutCommand?.NotifyCanExecuteChanged();
        SaveDraft();
    }

    private void NotifyCartStateChanged()
    {
        OnPropertyChanged(nameof(HasItemsInCart));
        OnPropertyChanged(nameof(CartItemCountDisplay));
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    private static decimal ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        string normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)
            ? Math.Max(0m, amount)
            : 0m;
    }

    public void CancelCurrentOrder()
    {
        if (Cart.Count == 0) return;
        _lastCleared = Snapshot();
        OnPropertyChanged(nameof(CanUndoClear));
        CreateNewOrder();
        StatusMessage = "Orden cancelada. Puedes deshacer.";
    }

    public void UndoClear()
    {
        if (_lastCleared is null) return;
        RestoreDraft(_lastCleared);
        _lastCleared = null;
        OnPropertyChanged(nameof(CanUndoClear));
        StatusMessage = "Orden restaurada";
    }

    public void HoldCurrentOrder()
    {
        if (Cart.Count == 0) return;
        _draftService.Hold(Snapshot());
        CreateNewOrder();
        StatusMessage = "Orden guardada en espera";
    }

    public IReadOnlyList<string> HeldOrders() => _draftService.HeldOrders();

    public bool RestoreHeldOrder(string name)
    {
        OrderDraftService.Draft? draft = _draftService.LoadHeld(name);
        if (draft is null) return false;
        RestoreDraft(draft);
        _draftService.DeleteHeld(name);
        StatusMessage = "Orden en espera recuperada";
        return true;
    }

    private OrderDraftService.Draft Snapshot() => new()
    {
        OrderNumber = OrderNumber,
        DiscountInput = DiscountInput,
        DiscountReason = DiscountReason,
        ReceivedCashInput = ReceivedCashInput,
        CardInput = CardInput,
        TransferInput = TransferInput,
        PaymentMethod = PaymentMethod,
        Items = Cart.Select(i => i.Model).ToList()
    };

    private void RestoreDraft(OrderDraftService.Draft draft)
    {
        _restoring = true;
        try
        {
            Cart.Clear();
            foreach (OrderItem item in draft.Items)
                Cart.Add(new OrderItemViewModel(item, RemoveItem, CalculateTotal));
            OrderNumber = draft.OrderNumber;
            DiscountInput = draft.DiscountInput;
            DiscountReason = draft.DiscountReason;
            ReceivedCashInput = draft.ReceivedCashInput;
            CardInput = draft.CardInput;
            TransferInput = draft.TransferInput;
            PaymentMethod = draft.PaymentMethod;
            NotifyCartStateChanged();
            CalculateTotal();
        }
        finally { _restoring = false; }
        SaveDraft();
    }

    private void SaveDraft()
    {
        if (!_loaded || _restoring) return;
        try { _draftService.SaveCurrent(Snapshot()); }
        catch (Exception ex) { StatusMessage = $"No se pudo proteger la orden: {ex.Message}"; }
    }
}
