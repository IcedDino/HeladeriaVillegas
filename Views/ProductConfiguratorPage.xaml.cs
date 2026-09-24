using HeladeriaPOS.Models;
using System.Globalization;

namespace HeladeriaPOS.Views;

public partial class ProductConfiguratorPage : ContentPage
{
    private readonly Product _product;
    private readonly TaskCompletionSource<ProductSelection?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _completed;
    private int _snackExtraCount;
    private int _extraScoopsCount;
    private int _containerIngredientCount;
    private int _otherIngredientCount;

    public ProductConfiguratorPage(Product product, IReadOnlyList<Flavor> flavors)
    {
        InitializeComponent();
        _product = product;
        ConfigureForProduct();
        foreach (Flavor flavor in flavors)
        {
            var button = new Button { Text = flavor.Name, Margin = new Thickness(0, 0, 8, 8), MinimumHeightRequest = 50 };
            button.Clicked += FlavorQuickClicked;
            FlavorButtons.Children.Add(button);
        }
        foreach (RadioButton radio in new[] { SnackNormalRadio, SnackPreparedRadio, SnackMissingRadio,
            SizeSmallRadio, SizeMediumRadio, SizeLargeRadio, SizeJumboRadio, SizeDoubleRadio, SizeTripleRadio,
            SizeHalfLiterRadio, SizeOneLiterRadio, SizeFiveLiterRadio, SizeTwelveLiterRadio,
            PreparationNoneRadio, PreparationSingleRadio, PreparationCombinedRadio })
            radio.CheckedChanged += (_, _) => UpdateSelectedPrice();
        CookedSwitch.Toggled += (_, _) => UpdateSelectedPrice();
        ChantillyCheck.CheckedChanged += (_, _) => UpdateSelectedPrice();
        UpdateSelectedPrice();
    }

    public Task<ProductSelection?> WaitForResultAsync() => _result.Task;

    private void ConfigureForProduct()
    {
        ProductNameText.Text = _product.Name;
        FlavorSection.IsVisible = _product.Category is ProductCategory.Helados or ProductCategory.Especialidades;
        SetPrice(SnackNormalRadio, "normal", "Normal");
        SetPrice(SnackPreparedRadio, "prepared", "Preparado completo");
        SetPrice(SnackMissingRadio, "missing", "Preparado sin ingrediente");
        SetPrice(SizeSmallRadio, "Chico", "Chico");
        SetPrice(SizeMediumRadio, "Mediano", "Mediano");
        SetPrice(SizeLargeRadio, "Grande", "Grande");
        SetPrice(SizeJumboRadio, "Jumbo", "Jumbo");
        SetPrice(SizeDoubleRadio, "Doble", "Doble");
        SetPrice(SizeTripleRadio, "Triple", "Triple");
        SetPrice(SizeHalfLiterRadio, "MedioLitro", "Medio litro");
        SetPrice(SizeOneLiterRadio, "UnLitro", "1 litro");
        SetPrice(SizeFiveLiterRadio, "CincoLitros", "5 litros");
        SetPrice(SizeTwelveLiterRadio, "DoceLitros", "12 litros");
        SetPrice(PreparationSingleRadio, "ice_single", "Un ingrediente");
        SetPrice(PreparationCombinedRadio, "ice_combined", "Chocolate o mermelada + cereal");
        string Money(string code) => Services.PriceCatalog.Get(_product, code).ToString("C", CultureInfo.GetCultureInfo("es-MX"));
        if (_product.ProductType == ProductType.SopaPalomitas)
            CookedPriceText.Text = $"Normal {Money("normal")} · Cocinada {Money("cooked")}";
        if (_product.Category == ProductCategory.Snacks && _product.ProductType != ProductType.Custom)
            SnackExtraPriceText.Text = $"{Money("snack_extra")} por unidad";
        if (_product.ProductType is ProductType.Barquillo or ProductType.Vaso or ProductType.Canasta)
            ScoopPriceText.Text = $"{Money("scoop")} por bola";
        if (_product.ProductType == ProductType.Envase)
            ContainerExtraPriceText.Text = $"{Money("container_extra")} por ingrediente (½ L y 1 L)";
        if (_product.Category == ProductCategory.Especialidades && _product.ProductType != ProductType.Custom)
        {
            ChantillyPriceText.Text = $"Crema Chantilly — +{Money("chantilly")}";
            SpecialExtraPriceText.Text = $"{Money("special_extra")} por unidad";
        }
        BaseHintText.Text = _product.ProductType switch
        {
            ProductType.Custom => $"Precio base {_product.BasePrice.ToString("C", CultureInfo.GetCultureInfo("es-MX"))}.",
            ProductType.PapasSabritas => "Elige Normal ($35), preparado completo ($65) o preparado sin algún ingrediente ($60).",
            ProductType.Fritura => "Precio $15. La salsa está incluida por defecto.",
            ProductType.SopaPalomitas => "Precio normal $30; cocinada $35.",
            ProductType.Barquillo or ProductType.Vaso => "Selecciona un tamaño y agrega preparación o bolas extra si lo deseas.",
            ProductType.Canasta => "Selecciona Doble o Triple y agrega modificadores si lo deseas.",
            ProductType.Envase => "Selecciona ½ L, 1 L, 5 L o 12 L. En ½ L y 1 L puedes agregar ingredientes de preparación.",
            ProductType.Malteada => "Malteada $40. Puedes agregar Chantilly u otros ingredientes.",
            _ => "Especialidad $70. Puedes agregar Chantilly u otros ingredientes."
        };
        if (_product.ProductType != ProductType.Custom)
            BaseHintText.Text = "Elige las opciones. El total se actualiza abajo con los precios vigentes.";

        bool isSnack = _product.Category == ProductCategory.Snacks;
        SnackExtrasSection.IsVisible = isSnack && _product.ProductType != ProductType.Custom;

        if (_product.ProductType == ProductType.Custom && _product.AllowsExtras)
        {
            CustomExtrasSection.IsVisible = true;
            CustomExtraNameText.Text = string.IsNullOrWhiteSpace(_product.ExtraName) ? "Extra" : _product.ExtraName;
            CustomExtraPriceText.Text = $"{_product.ExtraPrice.ToString("C", CultureInfo.GetCultureInfo("es-MX"))} por unidad";
        }

        switch (_product.ProductType)
        {
            case ProductType.PapasSabritas:
                SnackPreparationSection.IsVisible = true;
                break;
            case ProductType.SopaPalomitas:
                CookedSection.IsVisible = true;
                break;
            case ProductType.Barquillo:
            case ProductType.Vaso:
                CupSizeSection.IsVisible = true;
                IceCreamModifiersSection.IsVisible = true;
                break;
            case ProductType.Canasta:
                BasketSizeSection.IsVisible = true;
                IceCreamModifiersSection.IsVisible = true;
                break;
            case ProductType.Envase:
                ContainerSizeSection.IsVisible = true;
                break;
            case ProductType.Malteada:
            case ProductType.Copa:
            case ProductType.BananaSplit:
            case ProductType.TresMarias:
                SpecialtySection.IsVisible = true;
                break;
        }
    }

    private async void Add_Clicked(object? sender, EventArgs e)
    {
        IceCreamSize? size = ReadSelectedSize();
        bool requiresSize = _product.ProductType is ProductType.Barquillo or ProductType.Vaso or ProductType.Canasta or ProductType.Envase;

        if (requiresSize && size is null)
        {
            await DisplayAlert("Falta información", "Selecciona el tamaño antes de agregar el producto.", "Aceptar");
            return;
        }
        if (FlavorSection.IsVisible && string.IsNullOrWhiteSpace(FlavorsEntry.Text))
        {
            await DisplayAlert("Faltan sabores", "Selecciona al menos un sabor disponible.", "Aceptar");
            return;
        }

        var selection = new ProductSelection
        {
            SnackPreparation = ReadSnackPreparation(),
            Cooked = CookedSwitch.IsToggled,
            ExtraIngredientCount = _snackExtraCount,
            IceCreamSize = size,
            IceCreamPreparation = ReadIceCreamPreparation(),
            ExtraScoops = _extraScoopsCount,
            PreparationExtraIngredientCount = _containerIngredientCount,
            Chantilly = ChantillyCheck.IsChecked,
            OtherIngredientCount = _otherIngredientCount,
            Flavors = FlavorsEntry.Text?.Trim(),
            Instructions = InstructionsEntry.Text?.Trim()
        };

        _completed = true;
        _result.TrySetResult(selection);
    }

    private void Cancel_Clicked(object? sender, EventArgs e)
    {
        _completed = true;
        _result.TrySetResult(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_completed)
            _result.TrySetResult(null);
    }

    private SnackPreparation ReadSnackPreparation()
    {
        if (SnackPreparedRadio.IsChecked) return SnackPreparation.PreparedAll;
        if (SnackMissingRadio.IsChecked) return SnackPreparation.MissingIngredient;
        return SnackPreparation.Normal;
    }

    private IceCreamPreparation ReadIceCreamPreparation()
    {
        if (PreparationSingleRadio.IsChecked) return IceCreamPreparation.SingleIngredient;
        if (PreparationCombinedRadio.IsChecked) return IceCreamPreparation.ChocolateOrJamAndCereal;
        return IceCreamPreparation.None;
    }

    private IceCreamSize? ReadSelectedSize()
    {
        if (SizeSmallRadio.IsChecked) return IceCreamSize.Chico;
        if (SizeMediumRadio.IsChecked) return IceCreamSize.Mediano;
        if (SizeLargeRadio.IsChecked) return IceCreamSize.Grande;
        if (SizeJumboRadio.IsChecked) return IceCreamSize.Jumbo;
        if (SizeDoubleRadio.IsChecked) return IceCreamSize.Doble;
        if (SizeTripleRadio.IsChecked) return IceCreamSize.Triple;
        if (SizeHalfLiterRadio.IsChecked) return IceCreamSize.MedioLitro;
        if (SizeOneLiterRadio.IsChecked) return IceCreamSize.UnLitro;
        if (SizeFiveLiterRadio.IsChecked) return IceCreamSize.CincoLitros;
        if (SizeTwelveLiterRadio.IsChecked) return IceCreamSize.DoceLitros;
        return null;
    }

    private void SnackExtra_Clicked(object? sender, EventArgs e)
    {
        _snackExtraCount = ChangeCounter(_snackExtraCount, sender, 20);
        SnackExtraValue.Text = _snackExtraCount.ToString();
        UpdateSelectedPrice();
    }

    private void ExtraScoops_Clicked(object? sender, EventArgs e)
    {
        _extraScoopsCount = ChangeCounter(_extraScoopsCount, sender, 20);
        ExtraScoopsValue.Text = _extraScoopsCount.ToString();
        UpdateSelectedPrice();
    }

    private void ContainerIngredient_Clicked(object? sender, EventArgs e)
    {
        _containerIngredientCount = ChangeCounter(_containerIngredientCount, sender, 20);
        ContainerIngredientValue.Text = _containerIngredientCount.ToString();
        UpdateSelectedPrice();
    }

    private void OtherIngredient_Clicked(object? sender, EventArgs e)
    {
        _otherIngredientCount = ChangeCounter(_otherIngredientCount, sender, 30);
        OtherIngredientValue.Text = _otherIngredientCount.ToString();
        UpdateSelectedPrice();
    }

    private void CustomExtra_Clicked(object? sender, EventArgs e)
    {
        _otherIngredientCount = ChangeCounter(_otherIngredientCount, sender, 30);
        CustomExtraValue.Text = _otherIngredientCount.ToString();
        UpdateSelectedPrice();
    }

    private static int ChangeCounter(int current, object? sender, int maximum)
    {
        if (sender is not Button button)
            return current;

        if (button.Text == "+")
            return Math.Min(maximum, current + 1);

        return Math.Max(0, current - 1);
    }

    private void ContainerSize_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        bool canPrepare = SizeHalfLiterRadio.IsChecked || SizeOneLiterRadio.IsChecked;
        ContainerPreparationExtras.IsVisible = canPrepare;
        if (!canPrepare)
        {
            _containerIngredientCount = 0;
            ContainerIngredientValue.Text = "0";
        }
    }

    private void SetPrice(RadioButton radio, string code, string label)
    {
        ProductPrice? price = _product.Prices.FirstOrDefault(p => p.Code == code);
        if (price is not null)
            radio.Content = $"{label} — {price.Amount.ToString("C", CultureInfo.GetCultureInfo("es-MX"))}";
    }

    private ProductSelection CurrentSelection() => new()
    {
        SnackPreparation = ReadSnackPreparation(), Cooked = CookedSwitch.IsToggled,
        ExtraIngredientCount = _snackExtraCount, IceCreamSize = ReadSelectedSize(),
        IceCreamPreparation = ReadIceCreamPreparation(), ExtraScoops = _extraScoopsCount,
        PreparationExtraIngredientCount = _containerIngredientCount,
        Chantilly = ChantillyCheck.IsChecked, OtherIngredientCount = _otherIngredientCount
    };

    private void UpdateSelectedPrice()
    {
        try
        {
            PricingResult price = new Services.PricingService().Calculate(_product, CurrentSelection());
            decimal total = price.BasePrice + price.Modifiers.Sum(m => m.Total);
            SelectedPriceText.Text = $"Total por unidad: {total.ToString("C", CultureInfo.GetCultureInfo("es-MX"))}";
        }
        catch (InvalidOperationException)
        {
            SelectedPriceText.Text = "Selecciona un tamaño para ver el total";
        }
    }

    private void FlavorQuickClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;
        string current = FlavorsEntry.Text?.Trim() ?? string.Empty;
        FlavorsEntry.Text = current.Length == 0 ? button.Text : current + ", " + button.Text;
    }

    private void ClearFlavorsClicked(object? sender, EventArgs e) => FlavorsEntry.Text = string.Empty;
}
