using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTSharp.Maui.Pages
{
    public static class PagesExtensions
    {
        public static MauiAppBuilder ConfigurePages(this MauiAppBuilder builder)
        {
            // main tabs of the app
            builder.Services.AddSingleton<HomePage>(); 
            builder.Services.AddSingleton<SettingsPage>();
 
            return builder;
        }
    }

}
