using IoTSharp.Maui.Helpers;

namespace IoTSharp.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        TheTheme.SetTheme();

        if (AppConfig.Desktop)
            MainPage = new DesktopShell();
        else
            MainPage = new MobileShell();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        return new MauiWindow(MainPage);
    }
}
