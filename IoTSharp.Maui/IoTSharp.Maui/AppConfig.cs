namespace IoTSharp.Maui
{
    internal static class AppConfig
    {
        public static bool Desktop
        {
            get
            {
#if WINDOWS || MACCATALYST
            return true;
#else
                return false;
#endif
            }
        }
    }
}
