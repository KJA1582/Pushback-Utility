using Microsoft.Win32;
using Pushback_Utility.AppUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace Pushback_Utility
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static string FSXRegistry = "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Microsoft Games\\Flight Simulator\\10.0";
        private static string FSXSteamRegistry =
                            "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Microsoft Games\\Flight Simulator - Steam Edition\\10.0";
        internal string registryPath;
        void App_Startup(object sender, StartupEventArgs e)
        {
            // ADDITION FOR STEAM KEY THROUGH CONFIG FILE HERE
            string[] config = File.ReadAllLines("config.ini");
            if (config[0] == "FSXSE=1")
                Registry.GetValue(FSXSteamRegistry, "AppPath", null).ToString().Replace("\0", String.Empty);
            else
                registryPath = Registry.GetValue(FSXRegistry, "AppPath", null).ToString().Replace("\0", String.Empty);
            // Create main application window, starting minimized if specified
            MainWindow mainWindow = new MainWindow();
            //mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Show();
        }
    }
}
