using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;

namespace HeladeriaPOS.Views;

public partial class MainPage
{
    private bool _guideActive;
    private bool _guideBusy;
    private int _guideStep;
    private AbsoluteLayout? _guideOverlay;
    private Grid? _tutorialMenu;
    private readonly Dictionary<int, Border> _guideProducts = new();
    private Button? _guideQuantity;
    private View? _lastGuideTarget;
    private readonly List<BoxView> _guideShades = new();
    private Border? _guideHighlight;
    private Border? _guideInstruction;
#if WINDOWS
    private readonly Dictionary<Microsoft.UI.Xaml.Controls.Control, bool> _guideTabStops = new();
    private readonly List<Microsoft.UI.Xaml.Controls.ScrollViewer> _guideScrollers = new();
    private Microsoft.UI.Xaml.FrameworkElement? _guideObservedTarget;
#endif

    private static readonly string[] GuideInstructions =
    [
        "Toca el producto señalado para abrir sus opciones. En una venta puedes elegir cualquiera de los productos disponibles.",
        "El tamaño determina el precio. Toca la opción señalada y observa el total por unidad antes de agregar el producto.",
        "El sabor es opcional: puedes vender sin elegir uno. Toca el sabor señalado para practicar, o pulsa Continuar sin sabor. En ventas puedes elegir varios y tocar de nuevo para quitarlos.",
        "Pulsa Agregar al ticket para incluir el producto con las opciones elegidas. Esto todavía no cobra la venta.",
        "Pulsa + para agregar otra unidad del mismo producto. El total se recalcula. En ventas también puedes reducir con − o quitar el producto con ×.",
        "Revisa los productos, cantidades y total. Pulsa Cobrar para abrir las opciones de pago; la venta aún no se ha registrado.",
        "Selecciona Efectivo para esta práctica. En ventas también puedes cobrar con tarjeta, transferencia o combinar métodos con Mixto.",
        "Cada toque suma un billete al efectivo recibido. Toca el señalado hasta cubrir el total y observa el cambio. En ventas también puedes introducir el importe o usar Efectivo exacto.",
        "Comprueba el total, el efectivo recibido y el cambio que entregarás. Registrar venta confirma el cobro. Durante este tutorial solo completa la demo.",
        "¡Terminaste! La demo no guardó una venta real. Pulsa Finalizar para recuperar tu ticket original. Puedes repetir la guía desde Tutoriales."
    ];

    private void OnGuideProductLoaded(object? sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ProductCardViewModel card)
            _guideProducts[card.Model.Id] = border;
        if (_guideActive && _guideStep == 0) Dispatcher.Dispatch(RefreshGuide);
    }

    private void OnGuideQuantityLoaded(object? sender, EventArgs e)
    {
        _guideQuantity = sender as Button;
        if (_guideActive && _guideStep == 4) Dispatcher.Dispatch(RefreshGuide);
    }

    private void ShowTutorialMenu()
    {
        if (_tutorialMenu is not null) return;
        var progress = _tutorialProgress.LoadGuide();
        var body = new VerticalStackLayout { Spacing = 16 };
        body.Add(new Label { Text = "Tutoriales", FontSize = 26, FontAttributes = FontAttributes.Bold });
        body.Add(new Label { Text = "Cómo hacer una venta", FontSize = 21, FontAttributes = FontAttributes.Bold });
        body.Add(new Label { Text = "Practica en esta misma pantalla. Solo podrás tocar el control señalado. Tu ticket se conserva y la demo no registra ventas.", FontSize = 15 });
        body.Add(new Label { Text = progress.Completed ? "Completado · puedes repetirlo" :
            progress.Step > 0 ? $"En progreso · paso {progress.Step + 1} de 9" : "Listo para empezar", FontSize = 14 });
        var start = new Button { Text = progress.Completed ? "Repetir" : progress.Step > 0 ? "Continuar" : "Empezar",
            HeightRequest = 54, CornerRadius = 12, BackgroundColor = Color.FromArgb("#8E1244"), TextColor = Colors.White };
        start.Clicked += (_, _) => StartGuide(progress.Completed ? new() : progress);
        body.Add(start);
        var close = new Button { Text = "Cerrar", HeightRequest = 48, BackgroundColor = Color.FromArgb("#FFF0F3"), TextColor = Color.FromArgb("#8E1244") };
        close.Clicked += (_, _) => RemoveTutorialMenu();
        body.Add(close);
        _tutorialMenu = new Grid { BackgroundColor = Color.FromArgb("#99000000"), ZIndex = 500, Padding = 24 };
        _tutorialMenu.Add(new Border { Content = body, Padding = 24, MaximumWidthRequest = 520,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, BackgroundColor = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 } });
        Grid.SetColumnSpan(_tutorialMenu, 2);
        MainContent.Add(_tutorialMenu);
    }

    private void RemoveTutorialMenu()
    {
        if (_tutorialMenu is not null) MainContent.Remove(_tutorialMenu);
        _tutorialMenu = null;
    }

    private void StartGuide(TutorialProgressService.GuideProgress progress)
    {
        RemoveTutorialMenu();
        PaymentOverlay.IsVisible = NumericKeypadOverlay.IsVisible = HeldOrdersOverlay.IsVisible = false;
        _viewModel.BeginTutorial(progress.Step >= 4 ? progress.Practice : null);
        _guideStep = progress.Step;
        _guideActive = true;
        _viewModel.TutorialCheckoutCompleted += OnGuideCheckoutCompleted;
        MainContent.SizeChanged += OnGuideLayoutChanged;
        if (_guideStep >= 6) PaymentOverlay.IsVisible = true;
        if (_guideStep == 0)
        {
            var product = GuideProduct();
            if (product is null)
            {
                StopGuide();
                _ = DisplayAlert("Tutorial", "Activa un helado con tamaño en Precios para practicar esta venta.", "Aceptar");
                return;
            }
            ProductCollectionView.ScrollTo(product, position: ScrollToPosition.MakeVisible);
        }
        RefreshGuide();
    }

    private ProductCardViewModel? GuideProduct() => _viewModel.FilteredProducts.FirstOrDefault(p => p.Model.ProductType == ProductType.Vaso)
        ?? _viewModel.FilteredProducts.FirstOrDefault(p => p.Model.ProductType is ProductType.Barquillo or ProductType.Canasta or ProductType.Envase);

    private View? GuideTarget() => _guideStep switch
    {
        0 => GuideProduct() is { } product && _guideProducts.TryGetValue(product.Model.Id, out var card) ? card : null,
        1 or 2 or 3 => _activeProductConfigurator?.TutorialTarget(_guideStep),
        4 => _viewModel.Cart.Count > 0 && _guideQuantity?.BindingContext == _viewModel.Cart[0] ? _guideQuantity : null,
        5 => GuideOpenPayment,
        6 => GuideCashMethod,
        7 => GuideBill,
        8 => GuideCheckout,
        _ => null
    };

    private void RefreshGuide()
    {
        if (!_guideActive) return;
        if (_guideStep == 2 && _activeProductConfigurator?.TutorialTarget(2) is null) _guideStep = 3;
        if (_guideOverlay is not null) MainContent.Remove(_guideOverlay);
        _guideOverlay = new AbsoluteLayout { ZIndex = 600, InputTransparent = false };
        Grid.SetColumnSpan(_guideOverlay, 2);
        MainContent.Add(_guideOverlay);
        _guideShades.Clear();
        for (int i = 0; i < 4; i++)
        {
            var shade = new BoxView { Color = Color.FromArgb("#99000000") };
            _guideShades.Add(shade);
            _guideOverlay.Add(shade);
        }
        var proxy = new Button { Text = "", BackgroundColor = Colors.Transparent, BorderWidth = 0,
            Padding = 0, Opacity = 0.01, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        SemanticProperties.SetDescription(proxy, _guideStep < 9 ? GuideInstructions[_guideStep] : "Tutorial terminado");
        proxy.Clicked += async (_, _) => await PerformGuideAction();
        _guideHighlight = new Border { Content = proxy, Stroke = Color.FromArgb("#18B884"), StrokeThickness = 4,
            BackgroundColor = Color.FromArgb("#18FFFFFF"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
        _guideOverlay.Add(_guideHighlight);
        var instructions = new VerticalStackLayout { Spacing = 6 };
        instructions.Add(new Label { Text = _guideStep == 9 ? "Tutorial completado" : $"DEMO · Paso {_guideStep + 1} de 9",
            FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#8E1244") });
        instructions.Add(new Label { Text = GuideInstructions[_guideStep], FontSize = 15 });
        instructions.Add(new ProgressBar { Progress = _guideStep / 9d, ProgressColor = Color.FromArgb("#147D5B") });
        if (_guideStep == 2)
        {
            var skipFlavor = new Button { Text = "Continuar sin sabor", HeightRequest = 44, CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#EAF7F1"), TextColor = Color.FromArgb("#147D5B") };
            skipFlavor.Clicked += (_, _) => { _guideStep = 3; SaveGuideProgress(); RefreshGuide(); };
            instructions.Add(skipFlavor);
        }
        var exit = new Button { Text = _guideStep == 9 ? "Finalizar" : "Salir del tutorial", HeightRequest = 44,
            BackgroundColor = Color.FromArgb("#FFF0F3"), TextColor = Color.FromArgb("#8E1244"), CornerRadius = 10 };
        exit.Clicked += (_, _) => StopGuide();
        instructions.Add(exit);
        _guideInstruction = new Border { Content = instructions, Padding = 14, BackgroundColor = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 } };
        _guideOverlay.Add(_guideInstruction);
        var target = GuideTarget();
#if WINDOWS
        SuppressGuideTabStops(MainContent.Handler?.PlatformView as Microsoft.UI.Xaml.DependencyObject);
        if (target != _lastGuideTarget && target?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native)
            native.StartBringIntoView();
#endif
        _lastGuideTarget = target;
        LayoutGuide();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), LayoutGuide);
    }

    private void OnGuideLayoutChanged(object? sender, EventArgs e) => LayoutGuide();

    private void LayoutGuide()
    {
        if (!_guideActive || _guideOverlay is null || _guideHighlight is null || _guideInstruction is null) return;
        double width = Math.Max(1, MainContent.Width), height = Math.Max(1, MainContent.Height);
        var bounds = Rect.Zero;
#if WINDOWS
        if (GuideTarget()?.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native &&
            MainContent.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement root)
        {
            ObserveGuideScrolling(native);
            try
            {
                var point = native.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
                bounds = new Rect(point.X, point.Y, native.ActualWidth, native.ActualHeight);
            }
            catch (ArgumentException) { }
        }
#endif
        if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Bottom < 0 || bounds.Top > height)
        {
            _guideHighlight.IsVisible = false;
            AbsoluteLayout.SetLayoutBounds(_guideShades[0], new Rect(0, 0, width, height));
            for (int i = 1; i < 4; i++) AbsoluteLayout.SetLayoutBounds(_guideShades[i], Rect.Zero);
        }
        else
        {
            double x = Math.Clamp(bounds.X, 0, width), y = Math.Clamp(bounds.Y, 0, height);
            double right = Math.Clamp(bounds.Right, x, width), bottom = Math.Clamp(bounds.Bottom, y, height);
            _guideHighlight.IsVisible = true;
            AbsoluteLayout.SetLayoutBounds(_guideHighlight, new Rect(x, y, right - x, bottom - y));
            AbsoluteLayout.SetLayoutBounds(_guideShades[0], new Rect(0, 0, width, y));
            AbsoluteLayout.SetLayoutBounds(_guideShades[1], new Rect(0, bottom, width, height - bottom));
            AbsoluteLayout.SetLayoutBounds(_guideShades[2], new Rect(0, y, x, bottom - y));
            AbsoluteLayout.SetLayoutBounds(_guideShades[3], new Rect(right, y, width - right, bottom - y));
        }
        double guideWidth = Math.Min(420, width - 24);
        double guideX = bounds.X > width / 2 ? 12 : width - guideWidth - 12;
        double guideHeight = _guideStep == 2 ? 280 : 230;
        double guideY = bounds.Y < 200 ? Math.Max(12, height - guideHeight - 12) : 12;
        AbsoluteLayout.SetLayoutBounds(_guideInstruction, new Rect(guideX, guideY, guideWidth, guideHeight));
    }

    private async Task PerformGuideAction()
    {
        if (!_guideActive || _guideBusy || GuideTarget() is not { } target) return;
        _guideBusy = true;
        try
        {
            if (_guideStep == 0)
            {
                var product = GuideProduct();
                _guideBusy = false;
                if (product is not null) await product.SelectCommand.ExecuteAsync(null);
                return;
            }
            switch (target)
            {
                case Button button when button.IsEnabled:
                    ((IButtonController)button).SendClicked();
                    break;
                case ImageButton bill when bill.IsEnabled:
                    bill.Command?.Execute(bill.CommandParameter);
                    break;
                default: return;
            }
            if (_guideStep is 1 or 2 or 4 or 5 or 6) _guideStep++;
            else if (_guideStep == 7 && _viewModel.CheckoutCommand.CanExecute(null)) _guideStep = 8;
            SaveGuideProgress();
            RefreshGuide();
        }
        catch (Exception ex)
        {
            if (_guideActive) { _guideStep = _viewModel.Cart.Count > 0 ? 4 : 0; RefreshGuide(); }
            await DisplayAlert("Tutorial", ex.Message, "Aceptar");
        }
        finally { _guideBusy = false; }
    }

    private void OnGuideCheckoutCompleted(object? sender, EventArgs e)
    {
        _guideStep = 9;
        SaveGuideProgress();
        RefreshGuide();
    }

    private void SaveGuideProgress()
    {
        if (!_guideActive) return;
        try { _tutorialProgress.SaveGuide(new() { Step = _guideStep, Practice = _viewModel.TutorialSnapshot(), Completed = _guideStep == 9 }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _viewModel.StatusMessage = "No se pudo guardar el avance del tutorial."; }
    }

    private void StopGuide()
    {
        SaveGuideProgress();
        _guideActive = false;
        _guideBusy = false;
        _viewModel.TutorialCheckoutCompleted -= OnGuideCheckoutCompleted;
        MainContent.SizeChanged -= OnGuideLayoutChanged;
        _activeProductConfigurator?.Cancel();
        if (_guideOverlay is not null) MainContent.Remove(_guideOverlay);
        _guideOverlay = null;
        _lastGuideTarget = null;
        PaymentOverlay.IsVisible = ProductConfiguratorOverlay.IsVisible = false;
        _viewModel.EndTutorial();
#if WINDOWS
        foreach (var entry in _guideTabStops) entry.Key.IsTabStop = entry.Value;
        _guideTabStops.Clear();
        foreach (var scroll in _guideScrollers) scroll.ViewChanged -= OnGuideScrollChanged;
        _guideScrollers.Clear();
        _guideObservedTarget = null;
#endif
    }

#if WINDOWS
    private void ObserveGuideScrolling(Microsoft.UI.Xaml.FrameworkElement target)
    {
        if (ReferenceEquals(_guideObservedTarget, target)) return;
        foreach (var scroll in _guideScrollers) scroll.ViewChanged -= OnGuideScrollChanged;
        _guideScrollers.Clear();
        _guideObservedTarget = target;
        Microsoft.UI.Xaml.DependencyObject? node = target;
        while (node is not null)
        {
            if (node is Microsoft.UI.Xaml.Controls.ScrollViewer scroll)
            {
                scroll.ViewChanged += OnGuideScrollChanged;
                _guideScrollers.Add(scroll);
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
    }

    private void OnGuideScrollChanged(object? sender, Microsoft.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        => Dispatcher.Dispatch(LayoutGuide);

    private void SuppressGuideTabStops(Microsoft.UI.Xaml.DependencyObject? node)
    {
        if (node is null || ReferenceEquals(node, _guideOverlay?.Handler?.PlatformView)) return;
        if (node is Microsoft.UI.Xaml.Controls.Control control)
        {
            if (!_guideTabStops.ContainsKey(control)) _guideTabStops.Add(control, control.IsTabStop);
            control.IsTabStop = false;
        }
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++) SuppressGuideTabStops(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i));
    }
#endif
}
