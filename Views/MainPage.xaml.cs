using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;
using HeladeriaPOS.Models;
using System.Globalization;

namespace HeladeriaPOS.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly IProductDialogService _dialogService;
    private readonly OpenverseService _openverseService;
    private readonly TicketService _ticketService;
    private readonly TutorialProgressService _tutorialProgress;
    private OpenverseImageResult? _selectedOpenverseImage;
    private CancellationTokenSource? _openverseSearchCancellation;
    private bool _isProductConfiguratorOpen;
    private ProductConfiguratorPage? _activeProductConfigurator;

    private enum KeypadTarget
    {
        Cash,
        CustomSale,
        BasePrice,
        ExtraPrice,
        Discount,
        Card,
        Transfer
    }

    private string? _newProductCategoryName;
    private bool _updatingKeypadInput;
    private bool _replaceKeypadValue;
    private string _customSalePrice = "0";
    private KeypadTarget _keypadTarget = KeypadTarget.Cash;
    public MainPage(MainViewModel viewModel, IProductDialogService dialogService, OpenverseService openverseService, TicketService ticketService,
        TutorialProgressService tutorialProgress)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _dialogService = dialogService;
        _openverseService = openverseService;
        _ticketService = ticketService;
        _tutorialProgress = tutorialProgress;
        ProductCollectionView.SizeChanged += (s, e) => UpdateCardHeight();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Categories.CollectionChanged += (_, _) => RebuildCategoryButtons();
        RebuildCategoryButtons();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedCategoryName)) UpdateCategoryButtons();
        if (e.PropertyName == nameof(MainViewModel.HasItemsInCart) && !_viewModel.HasItemsInCart && PaymentOverlay.IsVisible)
            PaymentOverlay.IsVisible = false;
    }

    private void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string name })
            _viewModel.SelectedCategoryName = name;
    }

    private void RebuildCategoryButtons()
    {
        CategoryButtons.Children.Clear();
        foreach (string name in _viewModel.Categories)
        {
            var button = new Button
            {
                Text = name, CommandParameter = name,
                Style = (Style)Application.Current!.Resources["CategoryButtonStyle"],
                MinimumHeightRequest = 54, Padding = new Thickness(16, 10), Margin = new Thickness(0, 0, 8, 6)
            };
            button.Clicked += OnCategoryClicked;
            CategoryButtons.Children.Add(button);
        }
        UpdateCategoryButtons();
    }

    private void UpdateCategoryButtons()
    {
        foreach (Button button in CategoryButtons.Children.OfType<Button>())
        {
            bool selected = button.CommandParameter as string == _viewModel.SelectedCategoryName;
            button.BackgroundColor = Color.FromArgb(selected ? "#8E1244" : "#FFF4F7");
            button.TextColor = selected ? Colors.White : Color.FromArgb("#8E1244");
        }
    }

    private void OnOpenPaymentClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasItemsInCart)
            return;
        PaymentOverlay.IsVisible = true;
    }

    private void OnClosePaymentClicked(object? sender, EventArgs e)
    {
        PaymentOverlay.IsVisible = false;
    }

    private void OnTutorialsClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsBusy || _isProductConfiguratorOpen || _guideActive) return;
        ShowTutorialMenu();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartupLog.Write($"MainPage appearing products={_viewModel.Products.Count} filtered={_viewModel.FilteredProducts.Count} categories={_viewModel.Categories.Count} selected={_viewModel.SelectedCategoryName}");
        _dialogService.Attach(this);
    }

    public Task ShowStartupErrorAsync(Exception exception) =>
        DisplayAlert("No se pudo iniciar el POS", exception.Message, "Cerrar");

    public async Task<ProductSelection?> ShowProductConfiguratorAsync(HeladeriaPOS.Models.Product product, IReadOnlyList<HeladeriaPOS.Models.Flavor> flavors)
    {
        if (_isProductConfiguratorOpen)
            return null;

        _isProductConfiguratorOpen = true;
        try
        {
            var configurator = new ProductConfiguratorPage(product, flavors, (title, message) => DisplayAlert(title, message, "Aceptar"));
            _activeProductConfigurator = configurator;
            ProductConfiguratorContent.Content = configurator;
            ProductConfiguratorOverlay.IsVisible = true;
            if (_guideActive) { _guideStep = 1; RefreshGuide(); }
            return await configurator.WaitForResultAsync();
        }
        finally
        {
            ProductConfiguratorOverlay.IsVisible = false;
            ProductConfiguratorContent.Content = null;
            _activeProductConfigurator = null;
            _isProductConfiguratorOpen = false;
            if (_guideActive) Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                if (!_guideActive) return;
                _guideStep = _viewModel.Cart.Count > 0 ? 4 : 0;
                SaveGuideProgress();
                RefreshGuide();
            });
        }
    }

    private void OnProductConfiguratorBackdropTapped(object? sender, TappedEventArgs e) =>
        _activeProductConfigurator?.Cancel();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || MainContent is null || ProductGridLayout is null)
            return;

        if (height > 0 && ProductConfiguratorCard is not null)
        {
            ProductConfiguratorCard.WidthRequest = Math.Min(920, width * 0.86);
            ProductConfiguratorCard.HeightRequest = Math.Min(820, height * 0.88);
        }

        double sidebarWidth = width < 1250 ? 420 : 460;
        MainContent.ColumnDefinitions[1].Width = new GridLength(sidebarWidth);

        AddProductCard.WidthRequest = Math.Min(1060, width - 32);
        AddProductCard.HeightRequest = Math.Min(740, height - 32);
        ProductCategoryCard.WidthRequest = Math.Min(620, width - 32);
        ProductCategoryCard.HeightRequest = Math.Min(540, height - 32);
        CustomSaleCard.WidthRequest = Math.Min(620, width - 32);
        CustomSaleCard.HeightRequest = Math.Min(680, height - 32);
        bool compact = width < 1024;
        PaymentCard.Margin = compact ? new Thickness(16) : new Thickness(32);

        double catalogWidth = width - sidebarWidth - 56;
        int span = catalogWidth >= 1000 ? 4 : catalogWidth >= 660 ? 3 : 2;
        ProductGridLayout.Span = span;
        UpdateCardHeight();
    }

    private void OnCollectionViewSizeChanged(object? sender, EventArgs e)
    {
        UpdateCardHeight();
    }

    private void UpdateCardHeight()
    {
        if (ProductGridLayout is null || ProductCollectionView is null)
            return;

        double collectionViewWidth = ProductCollectionView.Width;
        if (collectionViewWidth <= 0)
            return;

        int span = ProductGridLayout.Span;
        if (span <= 0)
            span = 1;

        double itemSpacing = ProductGridLayout.HorizontalItemSpacing;
        double totalSpacing = itemSpacing * (span - 1);
        double itemSize = (collectionViewWidth - totalSpacing) / span;

        if (itemSize > 0 && Math.Abs(_viewModel.ProductCardHeight - itemSize) > 0.5)
        {
            _viewModel.ProductCardHeight = Math.Floor(itemSize);
        }
    }

    private void OnReceivedCashTapped(object? sender, TappedEventArgs e) => ShowNumericKeypad();

    private void OnDiscountClicked(object? sender, EventArgs e) => ShowKeypad(KeypadTarget.Discount, "DESCUENTO EN PESOS");
    private void OnCardTapped(object? sender, TappedEventArgs e) => ShowKeypad(KeypadTarget.Card, "PAGO CON TARJETA");
    private void OnTransferTapped(object? sender, TappedEventArgs e) => ShowKeypad(KeypadTarget.Transfer, "TRANSFERENCIA");

    private void OnBasePriceKeypadClicked(object? sender, EventArgs e) =>
        ShowKeypad(KeypadTarget.BasePrice, "PRECIO BASE");

    private void OnExtraPriceKeypadClicked(object? sender, EventArgs e) =>
        ShowKeypad(KeypadTarget.ExtraPrice, "PRECIO POR EXTRA");

    private void ShowNumericKeypad() => ShowKeypad(KeypadTarget.Cash, "EFECTIVO RECIBIDO");

    private void ShowKeypad(KeypadTarget target, string title)
    {
        _keypadTarget = target;
        KeypadTitle.Text = title;
        _replaceKeypadValue = true;
        KeypadError.IsVisible = false;
        UpdateKeypadInput();
        NumericKeypadOverlay.IsVisible = true;
    }

    private string GetKeypadValue() => _keypadTarget switch
    {
        KeypadTarget.CustomSale => _customSalePrice,
        KeypadTarget.BasePrice => NewProductPrice.Text ?? string.Empty,
        KeypadTarget.ExtraPrice => NewProductExtraPrice.Text ?? string.Empty,
        KeypadTarget.Discount => _viewModel.DiscountInput,
        KeypadTarget.Card => _viewModel.CardInput,
        KeypadTarget.Transfer => _viewModel.TransferInput,
        _ => _viewModel.ReceivedCashInput ?? "0"
    };

    private void SetKeypadValue(string value)
    {
        switch (_keypadTarget)
        {
            case KeypadTarget.CustomSale:
                _customSalePrice = value;
                CustomSalePrice.Text = "$" + value;
                CustomSaleError.IsVisible = false;
                break;
            case KeypadTarget.BasePrice:
                NewProductPrice.Text = value;
                break;
            case KeypadTarget.ExtraPrice:
                NewProductExtraPrice.Text = value;
                break;
            case KeypadTarget.Discount:
                _viewModel.DiscountInput = value;
                break;
            case KeypadTarget.Card:
                _viewModel.CardInput = value;
                break;
            case KeypadTarget.Transfer:
                _viewModel.TransferInput = value;
                break;
            default:
                _viewModel.ReceivedCashInput = value;
                break;
        }
        UpdateKeypadInput();
    }

    private void UpdateKeypadInput()
    {
        _updatingKeypadInput = true;
        try { KeypadValueInput.Text = GetKeypadValue(); }
        finally { _updatingKeypadInput = false; }
    }

    private void OnKeypadInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingKeypadInput || !NumericKeypadOverlay.IsVisible) return;
        _replaceKeypadValue = false;
        KeypadError.IsVisible = false;
        SetKeypadValue((e.NewTextValue ?? string.Empty).Trim().Replace(',', '.'));
    }

    private void OnKeypadNumberClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { Text: var key } || string.IsNullOrEmpty(key))
            return;

        if (_keypadTarget == KeypadTarget.CustomSale) CustomSaleName.Unfocus();
        string current = _replaceKeypadValue ? "0" : GetKeypadValue();
        _replaceKeypadValue = false;
        if (string.IsNullOrEmpty(current)) current = "0";
        if (key == ".")
        {
            if (!current.Contains('.'))
                SetKeypadValue(current + ".");
            return;
        }

        if (current.Contains('.') && current.Split('.')[1].Length >= 2) return;
        if (current.Replace(".", "").Length >= 7) return;

        SetKeypadValue(current == "0" ? key : current + key);
    }

    private void OnKeypadBackspaceClicked(object? sender, EventArgs e)
    {
        _replaceKeypadValue = false;
        if (_keypadTarget == KeypadTarget.CustomSale) CustomSaleName.Unfocus();
        string current = GetKeypadValue();
        if (string.IsNullOrEmpty(current))
            current = "0";
        SetKeypadValue(current.Length > 1 ? current[..^1] : "0");
    }

    private void OnKeypadClearClicked(object? sender, EventArgs e)
    {
        _replaceKeypadValue = false;
        if (_keypadTarget == KeypadTarget.CustomSale) CustomSaleName.Unfocus();
        SetKeypadValue("0");
    }

    private void OnKeypadDoneClicked(object? sender, EventArgs e)
    {
        string value = GetKeypadValue();
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount)
            || amount < 0m || amount > 1000000m || decimal.Round(amount, 2) != amount)
        {
            KeypadError.Text = "Escribe un importe de 0 a 1,000,000, con hasta dos decimales.";
            KeypadError.IsVisible = true;
            return;
        }
        SetKeypadValue(amount.ToString("0.##", CultureInfo.InvariantCulture));
        KeypadValueInput.Unfocus();
        NumericKeypadOverlay.IsVisible = false;
    }

    private void OnKeypadBackdropTapped(object? sender, TappedEventArgs e) => OnKeypadDoneClicked(sender, EventArgs.Empty);

    private void OnKeypadPanelTapped(object? sender, TappedEventArgs e)
    {
        // Impide que un toque dentro del panel cierre el teclado por propagación.
    }

    private async void OnCancelOrderClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasItemsInCart) return;
        if (await DisplayAlert("Cancelar orden", $"¿Vaciar {_viewModel.CartItemCountDisplay} de la orden {_viewModel.OrderDisplayName}?", "Cancelar orden", "Conservar"))
            _viewModel.CancelCurrentOrder();
    }

    private void OnUndoOrderClicked(object? sender, EventArgs e) => _viewModel.UndoClear();

    private void OnHoldOrderClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasItemsInCart || _viewModel.IsBusy) return;
        _viewModel.HoldCurrentOrder();
    }

    private void OnOpenHeldOrdersClicked(object? sender, EventArgs e)
    {
        _viewModel.RefreshHeldOrders();
        HeldOrdersOverlay.IsVisible = true;
    }

    private void OnCloseHeldOrdersClicked(object? sender, EventArgs e)
    {
        HeldOrdersOverlay.IsVisible = false;
    }

    private async void OnRestoreHeldFromOverlayClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string name) return;
        if (_viewModel.HasItemsInCart &&
            !await DisplayAlert("Orden actual", $"La orden actual ({_viewModel.OrderDisplayName}) se pondrá en espera para abrir {OrderDisplayNames.For(name)}. ¿Continuar?", "Continuar", "Volver"))
            return;
        if (_viewModel.HasItemsInCart) _viewModel.HoldCurrentOrder();
        _viewModel.RestoreHeldOrder(name);
        HeldOrdersOverlay.IsVisible = false;
    }

    private async void OnDeleteHeldClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string name) return;
        if (!await DisplayAlert("Eliminar en espera", $"¿Quitar la {OrderDisplayNames.For(name)} de la lista en espera?", "Eliminar", "Conservar")) return;
        _viewModel.DeleteHeldOrder(name);
    }

    private async void OnSalesClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(new SalesPage(_ticketService)));
    private async void OnPricesClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(new PricesPage(_ticketService, _viewModel, OpenAddProductForm)));

    public void OpenAddProductForm()
    {
        ResetAddProductForm();
        AddProductOverlay.IsVisible = true;
    }

    private void OnAddProductClicked(object? sender, EventArgs e) => OpenAddProductForm();

    private void OnCancelAddProductClicked(object? sender, EventArgs e)
    {
        AddProductOverlay.IsVisible = false;
    }

    private void OnNewProductAllowsExtrasToggled(object? sender, ToggledEventArgs e)
    {
        NewProductExtrasFields.IsVisible = e.Value;
    }

    private async void OnTakeProductPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(
                    "Cámara no disponible",
                    "Este equipo no permite tomar fotografías desde la aplicación. Puedes elegir una imagen guardada.",
                    "Entendido");
                return;
            }

            FileResult? photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Foto del producto"
            });
            await UsePersonalProductPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            ShowAddProductError($"No se pudo tomar la foto: {ex.Message}");
        }
    }

    private async void OnChooseProductPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            FileResult? photo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona una foto del producto",
                FileTypes = FilePickerFileType.Images
            });
            await UsePersonalProductPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            ShowAddProductError($"No se pudo abrir la imagen: {ex.Message}");
        }
    }

    private async Task UsePersonalProductPhotoAsync(FileResult? photo)
    {
        if (photo is null)
            return;

        string extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            extension = ".jpg";

        string directory = Path.Combine(FileSystem.AppDataDirectory, "ProductImages");
        Directory.CreateDirectory(directory);
        string localPath = Path.Combine(directory, $"personal_{Guid.NewGuid():N}{extension}");

        await using Stream input = await photo.OpenReadAsync();
        await using FileStream output = File.Create(localPath);
        await input.CopyToAsync(output);

        _selectedOpenverseImage = new OpenverseImageResult
        {
            Id = Path.GetFileNameWithoutExtension(localPath),
            Title = "Foto propia",
            Thumbnail = localPath,
            License = string.Empty
        };
        NewProductImagePreview.Source = localPath;
        NewProductImageAttribution.Text = "Foto propia guardada en este equipo";
        AddProductErrorPanel.IsVisible = false;
    }

    private async void OnSaveProductClicked(object? sender, EventArgs e)
    {
        AddProductErrorPanel.IsVisible = false;
        string name = NewProductName.Text?.Trim() ?? string.Empty;
        if (NewCategoryPanel.IsVisible)
        {
            ShowAddProductError("Agrega o cancela la nueva categor\u00eda antes de guardar el producto.");
            return;
        }

        if (name.Length == 0)
        {
            ShowAddProductError("Escribe el nombre del producto.");
            return;
        }

        if (!TryParseAmount(NewProductPrice.Text, out decimal basePrice) || basePrice <= 0m || basePrice > 100000m || decimal.Round(basePrice, 2) != basePrice)
        {
            ShowAddProductError("Escribe un precio base mayor que cero, hasta $100,000 y con hasta dos decimales.");
            return;
        }

        bool allowsExtras = NewProductAllowsExtras.IsToggled;
        decimal extraPrice = 0m;
        if (allowsExtras && (!TryParseAmount(NewProductExtraPrice.Text, out extraPrice) || extraPrice <= 0m || extraPrice > 100000m || decimal.Round(extraPrice, 2) != extraPrice))
        {
            ShowAddProductError("Escribe un precio por extra mayor que cero, hasta $100,000 y con hasta dos decimales.");
            return;
        }

        string? categoryName = _newProductCategoryName;
        Enum.TryParse(categoryName, out ProductCategory category);
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            ShowAddProductError("Selecciona una categoría.");
            return;
        }

        SaveProductButton.IsEnabled = false;
        try
        {
            await _viewModel.AddProductAsync(
                category,
                name,
                basePrice,
                NewProductTag.Text,
                allowsExtras,
                NewProductExtraName.Text,
                extraPrice,
                _selectedOpenverseImage, categoryName);

            AddProductOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            ShowAddProductError($"No se pudo guardar el producto: {ex.Message}");
        }
        finally
        {
            SaveProductButton.IsEnabled = true;
        }
    }

    private void SetProductCategory(string? name)
    {
        _newProductCategoryName = name;
        NewProductCategoryButton.Text = name ?? "Elegir categor\u00eda";
    }

    private void OnChooseProductCategoryClicked(object? sender, EventArgs e)
    {
        ProductCategoryChoices.Children.Clear();
        foreach (string name in _viewModel.Categories)
        {
            bool selected = name == _newProductCategoryName;
            var button = new Button
            {
                Text = name, MinimumHeightRequest = 64, FontSize = 20,
                Padding = new Thickness(16, 12), CornerRadius = 12,
                BackgroundColor = Color.FromArgb(selected ? "#8E1244" : "#FFF4F7"),
                TextColor = selected ? Colors.White : Color.FromArgb("#8E1244")
            };
            button.Clicked += (_, _) =>
            {
                SetProductCategory(name);
                ProductCategoryOverlay.IsVisible = false;
            };
            ProductCategoryChoices.Children.Add(button);
        }
        ProductCategoryOverlay.IsVisible = true;
    }

    private void OnCloseProductCategoriesClicked(object? sender, EventArgs e) => ProductCategoryOverlay.IsVisible = false;

    private void OnNewCategoryClicked(object? sender, EventArgs e)
    {
        ProductCategoryOverlay.IsVisible = false;
        NewCategoryName.Text = string.Empty;
        NewCategoryError.IsVisible = false;
        NewCategoryPanel.IsVisible = true;
    }

    private void OnCancelNewCategoryClicked(object? sender, EventArgs e)
    {
        NewCategoryName.Unfocus();
        NewCategoryPanel.IsVisible = false;
    }

    private async void OnCreateCategoryClicked(object? sender, EventArgs e)
    {
        if (!CreateCategoryButton.IsEnabled) return;
        CreateCategoryButton.IsEnabled = false;
        NewCategoryError.IsVisible = false;
        try
        {
            string saved = await _ticketService.AddCategoryAsync(NewCategoryName.Text ?? string.Empty);
            await _viewModel.RefreshCategoriesAsync();
            SetProductCategory(saved);
            AddProductErrorPanel.IsVisible = false;
            OnCancelNewCategoryClicked(sender, e);
        }
        catch (Exception ex)
        {
            NewCategoryError.Text = ex.Message;
            NewCategoryError.IsVisible = true;
        }
        finally { CreateCategoryButton.IsEnabled = true; }
    }

    private void OnCustomSaleClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsBusy || CustomSaleOverlay.IsVisible) return;
        CustomSaleName.Text = string.Empty;
        _keypadTarget = KeypadTarget.CustomSale;
        _replaceKeypadValue = false;
        SetKeypadValue("0");
        CustomSaleOverlay.IsVisible = true;
    }

    private void OnCustomSaleNameCompleted(object? sender, EventArgs e) => CustomSaleName.Unfocus();

    private void OnCancelCustomSaleClicked(object? sender, EventArgs e)
    {
        CustomSaleName.Unfocus();
        CustomSaleOverlay.IsVisible = false;
        _keypadTarget = KeypadTarget.Cash;
    }

    private void OnAddCustomSaleClicked(object? sender, EventArgs e)
    {
        if (!CustomSaleOverlay.IsVisible || _viewModel.IsBusy) return;
        try
        {
            string name = CustomSaleName.Text?.Trim() ?? string.Empty;
            if (name.Length == 0) throw new ArgumentException("Escribe el nombre del art\u00edculo.");
            if (!decimal.TryParse(_customSalePrice, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price)
                || price <= 0m || price > 100000m)
                throw new ArgumentException("Escribe un precio mayor que $0 y hasta $100,000.");
            _viewModel.AddCustomSale(name, price);
            OnCancelCustomSaleClicked(sender, e);
        }
        catch (ArgumentException ex)
        {
            CustomSaleError.Text = ex.Message;
            CustomSaleError.IsVisible = true;
        }
    }

    private void ResetAddProductForm()
    {
        SetProductCategory(_viewModel.Categories.Contains(_viewModel.SelectedCategoryName)
            ? _viewModel.SelectedCategoryName : _viewModel.Categories.FirstOrDefault());
        ProductCategoryOverlay.IsVisible = false;
        NewCategoryPanel.IsVisible = false;
        NewCategoryName.Text = string.Empty;
        NewCategoryError.IsVisible = false;
        NewProductName.Text = string.Empty;
        NewProductPrice.Text = "0";
        NewProductTag.Text = string.Empty;
        NewProductAllowsExtras.IsToggled = false;
        NewProductExtraName.Text = string.Empty;
        NewProductExtraPrice.Text = "0";
        _selectedOpenverseImage = null;
        NewProductImagePreview.Source = "placeholder.png";
        NewProductImageAttribution.Text = "Ninguna foto seleccionada";
        NewProductExtrasFields.IsVisible = false;
        AddProductErrorPanel.IsVisible = false;
    }

    private void ShowAddProductError(string message)
    {
        AddProductErrorText.Text = message;
        AddProductErrorPanel.IsVisible = true;
    }

    private static bool TryParseAmount(string? value, out decimal amount)
    {
        string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private void OnOpenversePickerClicked(object? sender, EventArgs e)
    {
        OpenverseGalleryOverlay.IsVisible = true;
        string productName = NewProductName.Text?.Trim() ?? string.Empty;
        string category = _newProductCategoryName ?? "Helados";
        OpenverseSearchEntry.Text = productName;
        ShowLocalLibrary(category);
    }

    private void OnCloseOpenverseClicked(object? sender, EventArgs e)
    {
        _openverseSearchCancellation?.Cancel();
        OpenverseGalleryOverlay.IsVisible = false;
    }

    private void OnOpenverseCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string category })
            return;

        OpenverseSearchEntry.Text = string.Empty;
        ShowLocalLibrary(category);
    }

    private async void OnOpenverseSearchClicked(object? sender, EventArgs e) => await SearchOpenverseAsync();

    private async void OnOpenverseSearchCompleted(object? sender, EventArgs e) => await SearchOpenverseAsync();

    private async Task SearchOpenverseAsync()
    {
        string query = OpenverseSearchEntry.Text?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            string category = _newProductCategoryName ?? "Helados";
            query = CategorySearchTerm(category);
            OpenverseSearchEntry.Text = query;
        }

        _openverseSearchCancellation?.Cancel();
        _openverseSearchCancellation?.Dispose();
        _openverseSearchCancellation = new CancellationTokenSource();

        OpenverseLoadingPanel.IsVisible = true;
        OpenverseStatusText.IsVisible = false;
        OpenverseResultsView.ItemsSource = null;

        try
        {
            IReadOnlyList<OpenverseImageResult> images = await _openverseService.SearchImagesAsync(
                query,
                20,
                _openverseSearchCancellation.Token);

            OpenverseResultsView.ItemsSource = images;
            if (images.Count == 0)
            {
                OpenverseStatusText.Text = "No se encontraron imágenes. Prueba con otra búsqueda.";
                OpenverseStatusText.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            OpenverseStatusText.Text = $"No se pudo consultar Openverse. Verifica la conexión.\n{ex.Message}";
            OpenverseStatusText.IsVisible = true;
        }
        finally
        {
            OpenverseLoadingPanel.IsVisible = false;
        }
    }

    private async void OnOpenverseImageSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not OpenverseImageResult image)
            return;

        OpenverseResultsView.SelectedItem = null;

        try
        {
            OpenverseLoadingPanel.IsVisible = !image.IsLocalLibraryImage;
            if (!image.IsLocalLibraryImage)
                image.Thumbnail = await _openverseService.CacheImageAsync(image);

            _selectedOpenverseImage = image;
            NewProductImagePreview.Source = image.Thumbnail;
            NewProductImageAttribution.Text = $"Imagen seleccionada: {image.DisplayTitle}";
            OpenverseGalleryOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            OpenverseStatusText.Text = $"No se pudo guardar la imagen en el equipo.\n{ex.Message}";
            OpenverseStatusText.IsVisible = true;
        }
        finally
        {
            OpenverseLoadingPanel.IsVisible = false;
        }
    }

    private void ShowLocalLibrary(string category)
    {
        _openverseSearchCancellation?.Cancel();
        OpenverseLoadingPanel.IsVisible = false;
        OpenverseStatusText.IsVisible = false;
        OpenverseResultsView.ItemsSource = ProductImageLibrary.Find(category);
    }

    private static string CategorySearchTerm(string category) => category switch
    {
        "Snacks" => "snack food chips popcorn",
        "Especialidades" => "milkshake sundae banana split dessert",
        _ => "ice cream gelato dessert"
    };
}
