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
    private readonly BackupService _backupService;
    private OpenverseImageResult? _selectedOpenverseImage;
    private CancellationTokenSource? _openverseSearchCancellation;
    private bool _loaded;

    private enum KeypadTarget
    {
        Cash,
        BasePrice,
        ExtraPrice,
        Discount,
        Card,
        Transfer
    }

    private KeypadTarget _keypadTarget = KeypadTarget.Cash;
    public MainPage(MainViewModel viewModel, IProductDialogService dialogService, OpenverseService openverseService, TicketService ticketService, BackupService backupService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _dialogService = dialogService;
        _openverseService = openverseService;
        _ticketService = ticketService;
        _backupService = backupService;
        ProductCollectionView.SizeChanged += (s, e) => UpdateCardHeight();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _dialogService.Attach(Navigation);

        if (_loaded)
            return;

        _loaded = true;
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo iniciar el POS", ex.Message, "Cerrar");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || MainContent is null || ProductGridLayout is null)
            return;

        double sidebarWidth = width < 1250 ? 420 : 460;
        MainContent.ColumnDefinitions[1].Width = new GridLength(sidebarWidth);

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

    private void OnReceivedCashFocused(object? sender, FocusEventArgs e) => ShowNumericKeypad();
    private void OnDiscountFocused(object? sender, FocusEventArgs e) => ShowKeypad(KeypadTarget.Discount, "DESCUENTO");
    private void OnDiscountTapped(object? sender, TappedEventArgs e) => ShowKeypad(KeypadTarget.Discount, "DESCUENTO");
    private void OnCardFocused(object? sender, FocusEventArgs e) => ShowKeypad(KeypadTarget.Card, "PAGO CON TARJETA");
    private void OnCardTapped(object? sender, TappedEventArgs e) => ShowKeypad(KeypadTarget.Card, "PAGO CON TARJETA");
    private void OnTransferFocused(object? sender, FocusEventArgs e) => ShowKeypad(KeypadTarget.Transfer, "TRANSFERENCIA");
    private void OnTransferTapped(object? sender, TappedEventArgs e) => ShowKeypad(KeypadTarget.Transfer, "TRANSFERENCIA");

    private void OnBasePriceTapped(object? sender, TappedEventArgs e) =>
        ShowKeypad(KeypadTarget.BasePrice, "PRECIO BASE");

    private void OnExtraPriceTapped(object? sender, TappedEventArgs e) =>
        ShowKeypad(KeypadTarget.ExtraPrice, "PRECIO POR EXTRA");

    private void ShowNumericKeypad() => ShowKeypad(KeypadTarget.Cash, "EFECTIVO RECIBIDO");

    private void ShowKeypad(KeypadTarget target, string title)
    {
        _keypadTarget = target;
        KeypadTitle.Text = title;
        if (target == KeypadTarget.Cash)
            ReceivedCashEntry.Unfocus();
        if (target == KeypadTarget.Discount) DiscountEntry.Unfocus();
        if (target == KeypadTarget.Card) CardEntry.Unfocus();
        if (target == KeypadTarget.Transfer) TransferEntry.Unfocus();
        NumericKeypadOverlay.IsVisible = true;
    }

    private string GetKeypadValue() => _keypadTarget switch
    {
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
    }

    private void OnKeypadNumberClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { Text: var key } || string.IsNullOrEmpty(key))
            return;

        string current = GetKeypadValue();
        if (string.IsNullOrEmpty(current))
            current = "0";

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
        string current = GetKeypadValue();
        if (string.IsNullOrEmpty(current))
            current = "0";
        SetKeypadValue(current.Length > 1 ? current[..^1] : "0");
    }

    private void OnKeypadClearClicked(object? sender, EventArgs e) => SetKeypadValue("0");

    private void OnKeypadDoneClicked(object? sender, EventArgs e) => NumericKeypadOverlay.IsVisible = false;

    private void OnKeypadBackdropTapped(object? sender, TappedEventArgs e) => NumericKeypadOverlay.IsVisible = false;

    private void OnKeypadPanelTapped(object? sender, TappedEventArgs e)
    {
        // Impide que un toque dentro del panel cierre el teclado por propagación.
    }

    private async void OnCancelOrderClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasItemsInCart) return;
        if (await DisplayAlert("Cancelar orden", $"¿Vaciar {_viewModel.CartItemCountDisplay} de la orden {_viewModel.OrderNumber}?", "Cancelar orden", "Conservar"))
            _viewModel.CancelCurrentOrder();
    }

    private void OnUndoOrderClicked(object? sender, EventArgs e) => _viewModel.UndoClear();

    private async void OnHoldOrderClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.HasItemsInCart) return;
        _viewModel.HoldCurrentOrder();
        await DisplayAlert("Orden en espera", "Puedes recuperarla desde el botón Recuperar.", "Aceptar");
    }

    private async void OnRestoreHeldClicked(object? sender, EventArgs e)
    {
        IReadOnlyList<string> orders = _viewModel.HeldOrders();
        if (orders.Count == 0) { await DisplayAlert("Órdenes en espera", "No hay órdenes guardadas.", "Aceptar"); return; }
        string? selected = await DisplayActionSheet("Recuperar orden", "Cerrar", null, orders.ToArray());
        if (selected is null || selected == "Cerrar") return;
        if (_viewModel.HasItemsInCart && !await DisplayAlert("Orden actual", "La orden actual será reemplazada. ¿Continuar?", "Continuar", "Volver")) return;
        if (_viewModel.HasItemsInCart) _viewModel.HoldCurrentOrder();
        _viewModel.RestoreHeldOrder(selected);
    }

    private async void OnSalesClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(new SalesPage(_ticketService)));
    private async void OnPricesClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(new PricesPage(_ticketService, _viewModel)));
    private async void OnBackupClicked(object? sender, EventArgs e)
    {
        try
        {
            string? choice = await DisplayActionSheet("Base de datos", "Cerrar", null, "Crear respaldo", "Restaurar respaldo");
            if (choice == "Crear respaldo")
            {
                string path = _backupService.CreateBackup();
                await DisplayAlert("Respaldo verificado", $"Copia creada en:\n{path}\n\nCópiala también a una USB o nube para protegerla si falla esta computadora.", "Aceptar");
            }
            else if (choice == "Restaurar respaldo")
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecciona una copia pos_*.db" });
                if (file is null) return;
                if (!await DisplayAlert("Restaurar ventas", "Al reiniciar, el respaldo reemplazará la base actual. Se conservará una copia de la base anterior. ¿Continuar?", "Preparar restauración", "Volver")) return;
                _backupService.ScheduleRestore(file.FullPath);
                await DisplayAlert("Restauración preparada", "Cierra y vuelve a abrir la aplicación para aplicar el respaldo.", "Aceptar");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error de respaldo", ex.Message, "Aceptar"); }
    }

    private void OnAddProductClicked(object? sender, EventArgs e)
    {
        ResetAddProductForm();
        AddProductOverlay.IsVisible = true;
    }

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

        if (name.Length == 0)
        {
            ShowAddProductError("Escribe el nombre del producto.");
            return;
        }

        if (!TryParseAmount(NewProductPrice.Text, out decimal basePrice) || basePrice <= 0m)
        {
            ShowAddProductError("Escribe un precio base mayor que cero.");
            return;
        }

        bool allowsExtras = NewProductAllowsExtras.IsToggled;
        decimal extraPrice = 0m;
        if (allowsExtras && (!TryParseAmount(NewProductExtraPrice.Text, out extraPrice) || extraPrice <= 0m))
        {
            ShowAddProductError("Escribe un precio por extra mayor que cero.");
            return;
        }

        if (!Enum.TryParse(NewProductCategory.SelectedItem?.ToString(), out ProductCategory category))
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
                _selectedOpenverseImage);

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

    private void ResetAddProductForm()
    {
        NewProductCategory.SelectedIndex = 0;
        NewProductName.Text = string.Empty;
        NewProductPrice.Text = string.Empty;
        NewProductTag.Text = string.Empty;
        NewProductAllowsExtras.IsToggled = false;
        NewProductExtraName.Text = string.Empty;
        NewProductExtraPrice.Text = string.Empty;
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
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private void OnOpenversePickerClicked(object? sender, EventArgs e)
    {
        OpenverseGalleryOverlay.IsVisible = true;
        string productName = NewProductName.Text?.Trim() ?? string.Empty;
        string category = NewProductCategory.SelectedItem?.ToString() ?? "Helados";
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
            string category = NewProductCategory.SelectedItem?.ToString() ?? "Helados";
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
