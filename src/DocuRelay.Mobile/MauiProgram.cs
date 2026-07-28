using DocuRelay.Mobile.Services;
using DocuRelay.Mobile.ViewModels;

namespace DocuRelay.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(ApiOptions.BaseUrl) });
        builder.Services.AddSingleton<DossierApiClient>();
        builder.Services.AddSingleton<SqliteOfflineStore>();
        builder.Services.AddSingleton<DossierRepository>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<DossiersViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
