using DocuRelay.Mobile.ViewModels;

namespace DocuRelay.Mobile;

public partial class MainPage : ContentPage
{
    private readonly DossiersViewModel _viewModel;

    public MainPage(DossiersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
