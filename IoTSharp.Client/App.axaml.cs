using AtomUI.Theme;
using AtomUI.Theme.Language;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IoTSharp.Client.Services;
using IoTSharp.Client.ViewModels;
using IoTSharp.Client.Views;

namespace IoTSharp.Client;

public partial class App : Application
{
    private readonly IoTSharpApiClient _apiClient = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        this.UseAtomUI(builder =>
        {
            builder.WithDefaultLanguageVariant(LanguageVariant.zh_CN);
            builder.WithDefaultTheme(IThemeManager.DEFAULT_THEME_ID);
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_apiClient)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
