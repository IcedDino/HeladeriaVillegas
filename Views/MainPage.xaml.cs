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
}
