using IoTSharp.Client.ViewModels;

namespace IoTSharp.Client;

public partial class MainPage : ContentPage
{
    public MainPage(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
