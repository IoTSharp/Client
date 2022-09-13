using IoTSharp.Maui.Resources.Strings;

namespace IoTSharp.Maui.ViewModels;

public class ShellViewModel : ViewModelBase
{
    public AppSection Home { get; set; }

    public AppSection Settings { get; set; }


    public ShellViewModel()
    {
        Home = new AppSection() { Title = AppResource.Home , Icon = "discover.png", IconDark="discover_dark.png", TargetType = typeof(HomePage) };
        Settings = new AppSection() { Title = AppResource.Settings, Icon = "settings.png", IconDark="settings_dark.png", TargetType = typeof(SettingsPage) };
    }
}
