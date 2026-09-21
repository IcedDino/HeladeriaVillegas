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
    private OpenverseImageResult? _selectedOpenverseImage;
    private CancellationTokenSource? _openverseSearchCancellation;
    private bool _loaded;
    public MainPage(MainViewModel viewModel, IProductDialogService dialogService, OpenverseService openverseService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _dialogService = dialogService;
        _openverseService = openverseService;
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

    private void ShowNumericKeypad()
    {
        ReceivedCashEntry.Unfocus();
        NumericKeypadOverlay.IsVisible = true;
    }

    private void OnKeypadNumberClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { Text: var key } || string.IsNullOrEmpty(key))
            return;

        string current = _viewModel.ReceivedCashInput ?? "0";

        if (key == ".")
        {
            if (!current.Contains('.'))
                _viewModel.ReceivedCashInput = current + ".";
            return;
        }

        _viewModel.ReceivedCashInput = current == "0" ? key : current + key;
    }

    private void OnKeypadBackspaceClicked(object? sender, EventArgs e)
    {
        string current = _viewModel.ReceivedCashInput ?? "0";
        _viewModel.ReceivedCashInput = current.Length > 1 ? current[..^1] : "0";
    }

    private void OnKeypadClearClicked(object? sender, EventArgs e) => _viewModel.ReceivedCashInput = "0";

    private void OnKeypadDoneClicked(object? sender, EventArgs e) => NumericKeypadOverlay.IsVisible = false;

    private void OnKeypadBackdropTapped(object? sender, TappedEventArgs e) => NumericKeypadOverlay.IsVisible = false;

    private void OnKeypadPanelTapped(object? sender, TappedEventArgs e)
    {
        // Impide que un toque dentro del panel cierre el teclado por propagación.
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

    private async void OnSaveProductClicked(object? sender, EventArgs e)
    {
        AddProductErrorText.IsVisible = false;
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
        NewProductImageAttribution.Text = "Sin imagen seleccionada";
        NewProductExtrasFields.IsVisible = false;
        AddProductErrorText.IsVisible = false;
    }

    private void ShowAddProductError(string message)
    {
        AddProductErrorText.Text = message;
        AddProductErrorText.IsVisible = true;
    }

    private static bool TryParseAmount(string? value, out decimal amount)
    {
        string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private async void OnOpenversePickerClicked(object? sender, EventArgs e)
    {
        OpenverseGalleryOverlay.IsVisible = true;
        string productName = NewProductName.Text?.Trim() ?? string.Empty;
        string category = NewProductCategory.SelectedItem?.ToString() ?? "Helados";
        OpenverseSearchEntry.Text = string.IsNullOrWhiteSpace(productName)
            ? CategorySearchTerm(category)
            : $"{productName} {CategorySearchTerm(category)}";
        await SearchOpenverseAsync();
    }

    private void OnCloseOpenverseClicked(object? sender, EventArgs e)
    {
        _openverseSearchCancellation?.Cancel();
        OpenverseGalleryOverlay.IsVisible = false;
    }

    private async void OnOpenverseCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string category })
            return;

        OpenverseSearchEntry.Text = CategorySearchTerm(category);
        await SearchOpenverseAsync();
    }

    private async void OnOpenverseSearchClicked(object? sender, EventArgs e) => await SearchOpenverseAsync();

    private async void OnOpenverseSearchCompleted(object? sender, EventArgs e) => await SearchOpenverseAsync();

    private async Task SearchOpenverseAsync()
    {
        string query = OpenverseSearchEntry.Text?.Trim() ?? string.Empty;
        if (query.Length == 0)
            return;

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

    private void OnOpenverseImageSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not OpenverseImageResult image)
            return;

        _selectedOpenverseImage = image;
        NewProductImagePreview.Source = image.Thumbnail;
        NewProductImageAttribution.Text = image.AttributionSummary;
        OpenverseGalleryOverlay.IsVisible = false;
        OpenverseResultsView.SelectedItem = null;
    }

    private static string CategorySearchTerm(string category) => category switch
    {
        "Snacks" => "snack food chips popcorn",
        "Especialidades" => "milkshake sundae banana split dessert",
        _ => "ice cream gelato dessert"
    };
}
