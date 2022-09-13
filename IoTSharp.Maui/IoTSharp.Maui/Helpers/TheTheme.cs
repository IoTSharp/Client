namespace IoTSharp.Maui.Helpers
{
    public static class TheTheme
    {
        public static void SetTheme()
        {
            switch (Settings.Theme)
            {
                default:
                case AppTheme.Light:
                    App.Current.UserAppTheme = AppTheme.Light;
                    break;
                case AppTheme.Dark:
                    App.Current.UserAppTheme = AppTheme.Dark;
                    break;

            }

            MessagingCenter.Instance.Send<string>("IoTSharp", "ChangeWebTheme");
        }
    }
}
