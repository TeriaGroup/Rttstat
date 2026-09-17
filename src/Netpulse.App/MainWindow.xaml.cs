using System.Windows;
using Netpulse.App.Theme;
using Netpulse.App.ViewModels;
using Netpulse.Core.Abstractions;

namespace Netpulse.App;

public partial class MainWindow : Window
{
    private readonly ISettingsProvider _settings;

    public MainWindow(MainViewModel vm, ISettingsProvider settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = vm;
        var w = settings.Current.MainWindow;
        Width = w.Width;
        Height = w.Height;
        if (w.Left is { } left && w.Top is { } top)
        {
            var work = SystemParameters.WorkArea;
            if (left < work.Right - 80 && top < work.Bottom - 80 && left > work.Left - Width + 80 && top > work.Top - 40)
            {
                Left = left;
                Top = top;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }
        if (w.Maximized) WindowState = WindowState.Maximized;
        DarkTitleBar.Apply(this);
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        var s = _settings.Current.MainWindow;
        s.Width = Width;
        s.Height = Height;
        s.Left = Left;
        s.Top = Top;
        s.Maximized = WindowState == WindowState.Maximized;
        _settings.Save();
        Hide();
    }
}
