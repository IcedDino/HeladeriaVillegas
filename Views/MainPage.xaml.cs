using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;

namespace HeladeriaPOS.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly IProductDialogService _dialogService;
    private bool _loaded;
    public MainPage(MainViewModel viewModel, IProductDialogService dialogService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _dialogService = dialogService;
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
        if (width <= 0 || PosLayout is null || ProductGridLayout is null)
            return;

        double sidebarWidth = width < 1250 ? 420 : 460;
        PosLayout.ColumnDefinitions[1].Width = new GridLength(sidebarWidth);
        double catalogWidth = width - sidebarWidth - 56;
        ProductGridLayout.Span = catalogWidth >= 1000 ? 4 : catalogWidth >= 660 ? 3 : 2;
    }
}
