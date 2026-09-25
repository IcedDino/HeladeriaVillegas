using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using System.Globalization;

namespace HeladeriaPOS.Views;

public partial class ProductConfiguratorPage : ContentPage
{
    private static readonly Color SelectedColor = Color.FromArgb("#C2185B");
    private static readonly Color UnselectedColor = Color.FromArgb("#2B1720");
    private static readonly Color UnselectedBorder = Color.FromArgb("#E7C6D2");

    private readonly Product _product;
    private readonly TaskCompletionSource<ProductSelection?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, List<Button>> _chipGroups = new();
    private readonly Dictionary<string, string> _selectedByGroup = new();
    private readonly List<Button> _flavorChips = new();
    private readonly List<string> _selectedFlavors = new();
    private bool _completed;
    private int _snackExtraCount;
    private int _extraScoopsCount;
    private int _containerIngredientCount;
    private int _otherIngredientCount;

    public ProductConfiguratorPage(Product product, IReadOnlyList<Flavor> flavors)
    {
        InitializeComponent();

        _product = product;
        GroupChips("SnackPreparation", SnackNormalChip, SnackPreparedChip, SnackMissingChip);
        GroupChips("IceCreamPreparation", PrepNoneChip, PrepSingleChip, PrepCombinedChip);
        GroupChips("IceCreamSize", SizeSmallChip, SizeMediumChip, SizeLargeChip, SizeJumboChip,
            SizeDoubleChip, SizeTripleChip, SizeHalfLiterChip, SizeOneLiterChip, SizeFiveLiterChip, SizeTwelveLiterChip);

        SelectChip("SnackPreparation", "normal");
        SelectChip("IceCreamPreparation", "None");

        foreach (Flavor flavor in flavors)
        {
            var chip = new Button
            {
                Text = flavor.Name,
                AutomationId = $"Flavor|{flavor.Name}",
                Margin = new Thickness(0, 0, 8, 8),
                Style = (Style)Resources["ChipStyle"]
            };
            chip.Clicked += ChipClicked;
            _flavorChips.Add(chip);
            FlavorChips.Children.Add(chip);
        }

        CookedSwitch.Toggled += (_, _) => UpdateSelectedPrice();
        ChantillyCheck.CheckedChanged += (_, _) => UpdateSelectedPrice();
        ConfigureForProduct();
        UpdateSelectedPrice();
    }

    public Task<ProductSelection?> WaitForResultAsync() => _result.Task;

    private static (string Group, string Value) SplitId(string id)
    {
        int index = id.IndexOf('|');
        return index < 0 ? (id, string.Empty) : (id[..index], id[(index + 1)..]);
    }

    private void GroupChips(string group, params Button[] chips)
    {
        var list = new List<Button>(chips);
        _chipGroups[group] = list;
        foreach (Button chip in list)
            chip.Clicked += ChipClicked;
    }

    private void SelectChip(string group, string value)
    {
        _selectedByGroup[group] = value;
        if (!_chipGroups.TryGetValue(group, out List<Button>? chips))
            return;
        foreach (Button chip in chips)
        {
            var (_, chipValue) = SplitId(chip.AutomationId);
            SetChipVisual(chip, chipValue == value);
        }
    }

    private static void SetChipVisual(Button chip, bool selected)
    {
        chip.BackgroundColor = selected ? SelectedColor : Colors.White;
        chip.TextColor = selected ? Colors.White : UnselectedColor;
        chip.BorderColor = selected ? SelectedColor : UnselectedBorder;
    }

    private void ChipClicked(object? sender, EventArgs e)
    {
        if (sender is not Button chip || string.IsNullOrEmpty(chip.AutomationId))
            return;

        var (group, value) = SplitId(chip.AutomationId);
        if (group == "Flavor")
        {
            ToggleFlavor(chip, value);
            return;
        }

        if (!_chipGroups.TryGetValue(group, out List<Button>? chips))
            return;

        foreach (Button other in chips)
            SetChipVisual(other, other == chip);
        _selectedByGroup[group] = value;

        if (group == "IceCreamSize")
            OnContainerSizeChanged();
        UpdateSelectedPrice();
    }

    private void ToggleFlavor(Button chip, string name)
    {
        if (_selectedFlavors.Contains(name))
        {
            _selectedFlavors.Remove(name);
            SetChipVisual(chip, false);
        }
        else
        {
            _selectedFlavors.Add(name);
            SetChipVisual(chip, true);
        }
        FlavorSummaryText.Text = _selectedFlavors.Count == 0
            ? "Ningún sabor elegido todavía."
            : string.Join(" · ", _selectedFlavors);
    }

    private void ConfigureForProduct()
    {
        ProductNameText.Text = _product.Name;
        if (!string.IsNullOrWhiteSpace(_product.ImagePath))
            ProductImage.Source = _product.ImagePath;
        else
            ProductImageFrame.IsVisible = false;

        FlavorSection.IsVisible = _product.Category is ProductCategory.Helados or ProductCategory.Especialidades;
        SetChipPrice(SnackNormalChip, "normal", "Normal");
        SetChipPrice(SnackPreparedChip, "prepared", "Preparado completo");
        SetChipPrice(SnackMissingChip, "missing", "Sin algún ingrediente");
        SetChipPrice(SizeSmallChip, "Chico", "Chico");
        SetChipPrice(SizeMediumChip, "Mediano", "Mediano");
        SetChipPrice(SizeLargeChip, "Grande", "Grande");
        SetChipPrice(SizeJumboChip, "Jumbo", "Jumbo");
        SetChipPrice(SizeDoubleChip, "Doble", "Doble");
        SetChipPrice(SizeTripleChip, "Triple", "Triple");
        SetChipPrice(SizeHalfLiterChip, "MedioLitro", "Medio litro");
        SetChipPrice(SizeOneLiterChip, "UnLitro", "1 litro");
        SetChipPrice(SizeFiveLiterChip, "CincoLitros", "Bote 5 lts");
        SetChipPrice(SizeTwelveLiterChip, "DoceLitros", "Bote 12 lts");
        SetChipPrice(PrepSingleChip, "ice_single", "Un solo ingrediente");
        SetChipPrice(PrepCombinedChip, "ice_combined", "Chocolate o mermelada + cereal");

        string Money(string code) => PriceCatalog.Get(_product, code).ToString("C", CultureInfo.GetCultureInfo("es-MX"));
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
            ProductType.PapasSabritas => "Elige Normal, preparado completo o sin algún ingrediente.",
            ProductType.Fritura => "Precio fijo. La salsa está incluida por defecto.",
            ProductType.SopaPalomitas => "Van normal o cocinadas.",
            ProductType.Barquillo or ProductType.Vaso => "Selecciona un tamaño y agrega preparación o bolas extra si lo deseas.",
            ProductType.Canasta => "Selecciona Doble o Triple y agrega modificadores si lo deseas.",
            ProductType.Envase => "Selecciona ½ L, 1 L, 5 L o 12 L. En ½ L y 1 L puedes agregar ingredientes de preparación.",
            ProductType.Malteada => "Malteada clásica. Puedes agregar Chantilly u otros ingredientes.",
            _ => "Especialidad clásica. Puedes agregar Chantilly u otros ingredientes."
        };

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
        if (FlavorSection.IsVisible && _selectedFlavors.Count == 0)
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
            Flavors = string.Join(", ", _selectedFlavors)
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

    private void ChantillyRowTapped(object? sender, TappedEventArgs e) => ChantillyCheck.IsChecked = !ChantillyCheck.IsChecked;

    private SnackPreparation ReadSnackPreparation() => _selectedByGroup.GetValueOrDefault("SnackPreparation") switch
    {
        "prepared" => SnackPreparation.PreparedAll,
        "missing" => SnackPreparation.MissingIngredient,
        _ => SnackPreparation.Normal
    };

    private IceCreamPreparation ReadIceCreamPreparation() => _selectedByGroup.GetValueOrDefault("IceCreamPreparation") switch
    {
        "SingleIngredient" => IceCreamPreparation.SingleIngredient,
        "ChocolateOrJamAndCereal" => IceCreamPreparation.ChocolateOrJamAndCereal,
        _ => IceCreamPreparation.None
    };

    private IceCreamSize? ReadSelectedSize() => _selectedByGroup.GetValueOrDefault("IceCreamSize") switch
    {
        "Chico" => IceCreamSize.Chico,
        "Mediano" => IceCreamSize.Mediano,
        "Grande" => IceCreamSize.Grande,
        "Jumbo" => IceCreamSize.Jumbo,
        "Doble" => IceCreamSize.Doble,
        "Triple" => IceCreamSize.Triple,
        "MedioLitro" => IceCreamSize.MedioLitro,
        "UnLitro" => IceCreamSize.UnLitro,
        "CincoLitros" => IceCreamSize.CincoLitros,
        "DoceLitros" => IceCreamSize.DoceLitros,
        _ => null
    };

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

    private void OnContainerSizeChanged()
    {
        bool canPrepare = _selectedByGroup.GetValueOrDefault("IceCreamSize") is "MedioLitro" or "UnLitro";
        ContainerPreparationExtras.IsVisible = canPrepare;
        if (!canPrepare)
        {
            _containerIngredientCount = 0;
            ContainerIngredientValue.Text = "0";
        }
    }

    private void SetChipPrice(Button chip, string code, string label)
    {
        decimal amount = (_product.Prices.FirstOrDefault(p => p.Code == code)?.Amount
            ?? PriceCatalog.Defaults(_product.ProductType).FirstOrDefault(p => p.Code == code)?.Amount)
            ?? 0m;
        if (amount > 0)
            chip.Text = $"{label} — {amount.ToString("C", CultureInfo.GetCultureInfo("es-MX"))}";
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
            PricingResult price = new PricingService().Calculate(_product, CurrentSelection());
            decimal total = price.BasePrice + price.Modifiers.Sum(m => m.Total);
            SelectedPriceText.Text = $"Total por unidad: {total.ToString("C", CultureInfo.GetCultureInfo("es-MX"))}";
        }
        catch (InvalidOperationException)
        {
            SelectedPriceText.Text = "Selecciona un tamaño para ver el total";
        }
    }

    private void ClearFlavorsClicked(object? sender, EventArgs e)
    {
        _selectedFlavors.Clear();
        foreach (Button chip in _flavorChips)
            SetChipVisual(chip, false);
        FlavorSummaryText.Text = "Ningún sabor elegido todavía.";
    }
}