namespace IoTSharp.Maui.Pages
{
    public partial class MobileShell
    {
        public MobileShell()
        {
            InitializeComponent();

            BindingContext = new ShellViewModel();
        }
    }
}
