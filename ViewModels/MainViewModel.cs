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
    private readonly OpenverseService _openverseService;
    private bool _loaded;

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

    public string SubtotalBaseDisplay => SubtotalBase.ToString("C0", CurrencyCulture);
    public string TotalExtrasDisplay => TotalExtras.ToString("C0", CurrencyCulture);
    public string DiscountDisplay => DiscountApplied.ToString("C0", CurrencyCulture);
    public string TotalDisplay => Total.ToString("C0", CurrencyCulture);
    public string ChangeDisplay => Change.ToString("C0", CurrencyCulture);
    public string CheckoutLabel => $"Cobrar & Registrar  ·  {TotalDisplay}";
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
        OpenverseService openverseService)
    {
        _databaseInitializer = databaseInitializer;
        _pricingService = pricingService;
        _dialogService = dialogService;
        _ticketService = ticketService;
        _openverseService = openverseService;
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

            try
            {
                await AssignOpenverseImagesAsync(products);
            }
            catch
            {
                // La conexión de imágenes es opcional; el POS debe seguir funcionando sin internet.
            }

            Products.Clear();
            for (int i = 0; i < products.Count; i++)
                Products.Add(new ProductCardViewModel(products[i], SelectProductAsync));

            ApplyCategoryFilter();
            _loaded = true;
            StatusMessage = "Listo para vender";
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

    private async Task AssignOpenverseImagesAsync(List<Product> products)
    {
        Product[] missing = products
            .Where(product => string.IsNullOrWhiteSpace(product.ImagePath) || product.ImagePath == "placeholder.png")
            .ToArray();

        if (missing.Length == 0)
            return;

        var searches = missing
            .Select(async product =>
            {
                string query = product.ProductType switch
                {
                    ProductType.PapasSabritas => "doritos",
                    ProductType.Fritura => "pork rinds snack",
                    ProductType.SopaPalomitas when product.Name.StartsWith("Sopa", StringComparison.OrdinalIgnoreCase) => "instant noodle soup",
                    ProductType.SopaPalomitas => "popcorn bowl",
                    ProductType.Barquillo => "ice cream cone",
                    ProductType.Vaso => "ice cream in a plastic cup",
                    ProductType.Canasta => "ice cream waffle bowl",
                    ProductType.Envase => "ice cream pint",
                    ProductType.Malteada => "milkshake",
                    ProductType.Copa => "ice cream sundae glass",
                    ProductType.BananaSplit => "banana split dessert",
                    ProductType.TresMarias => "ice cream three scoops",
                    _ => product.Name
                };

                IReadOnlyList<OpenverseImageResult> images = await _openverseService.SearchImagesAsync(query, 1);
                if (images.Count > 0)
                    ApplyImage(product, images[0]);
            });

        await Task.WhenAll(searches);
        Product[] updated = missing.Where(product => product.ImagePath != "placeholder.png").ToArray();
        if (updated.Length > 0)
            await _ticketService.UpdateProductsAsync(updated);
    }

    private static void ApplyImage(Product product, OpenverseImageResult image)
    {
        product.ImagePath = image.Thumbnail;
        product.ImageCreator = image.Creator;
        product.ImageLicense = $"{image.License.ToUpperInvariant()} {image.LicenseVersion}".Trim();
        product.ImageLicenseUrl = image.LicenseUrl;
        product.ImageSourceUrl = image.SourceUrl;
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
        };

        Cart.Add(new OrderItemViewModel(model, RemoveItem, CalculateTotal));
        NotifyCartStateChanged();
        CalculateTotal();
        StatusMessage = $"{productCard.Name} agregado al ticket";
    }

    private void RemoveItem(OrderItemViewModel item)
    {
        Cart.Remove(item);
        NotifyCartStateChanged();
        CalculateTotal();
        StatusMessage = "Partida eliminada";
    }

    partial void OnDiscountInputChanged(string value)
    {
        DiscountApplied = ParseMoney(value);
        CalculateTotal();
    }

    partial void OnReceivedCashInputChanged(string value)
    {
        CalculateChange();
        CheckoutCommand.NotifyCanExecuteChanged();
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
        Total = Math.Max(0m, beforeDiscount - DiscountApplied);

        CalculateChange();
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    private void CalculateChange()
    {
        decimal received = ParseMoney(ReceivedCashInput);
        Change = received > Total ? received - Total : 0m;
    }

    private bool CanCheckout()
    {
        decimal received = ParseMoney(ReceivedCashInput);
        return !IsBusy && Cart.Count > 0 && Total > 0m && received >= Total;
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
                Discount = DiscountApplied,
                Total = Total,
                Received = ParseMoney(ReceivedCashInput),
                Change = Change,
                Items = new List<OrderItem>(Cart.Count)
            };

            for (int i = 0; i < Cart.Count; i++)
                ticket.Items.Add(Cart[i].Model);

            await _ticketService.SaveTicketAsync(ticket);
            string paidOrder = OrderNumber;
            CreateNewOrder();
            StatusMessage = $"Venta {paidOrder} registrada correctamente";
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
        Change = 0m;
        OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd-HHmmssfff}";
        NotifyCartStateChanged();
        CheckoutCommand?.NotifyCanExecuteChanged();
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
}
