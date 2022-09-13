using Microsoft.AspNetCore.Components.WebView.Maui;
using IoTSharp.Shared.Data;
using IoTSharp.Maui.Services;

namespace IoTSharp.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .ConfigureEssentials()
            .ConfigurePages()
            .ConfigureViewModels()
            .ConfigureServices()
            .ConfigureFonts(fonts =>
			{
                fonts.AddFont("Segoe-Ui-Bold.ttf", "SegoeUiBold");
                fonts.AddFont("Segoe-Ui-Regular.ttf", "SegoeUiRegular");
                fonts.AddFont("Segoe-Ui-Semibold.ttf", "SegoeUiSemibold");
                fonts.AddFont("Segoe-Ui-Semilight.ttf", "SegoeUiSemilight");
            });
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMasaBlazor();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        return builder.Build();
	}
}
