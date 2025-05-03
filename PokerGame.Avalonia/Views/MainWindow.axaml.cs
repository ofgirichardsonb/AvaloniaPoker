using Avalonia;
using Avalonia.Controls;
using System;
using System.Diagnostics;

namespace PokerGame.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // We've completely removed the icon in the XAML to avoid any resource loading issues
                Console.WriteLine("MainWindow initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing MainWindow: {ex.Message}");
                Console.WriteLine($"Error initializing MainWindow: {ex}");
                
                // Continue without crashing
            }
        }
    }
}