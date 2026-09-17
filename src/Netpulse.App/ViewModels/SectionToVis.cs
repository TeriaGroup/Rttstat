using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Netpulse.App.ViewModels;

public sealed class SectionToVis : IValueConverter
{
    public static readonly SectionToVis Dash = new("Dashboard");
    public static readonly SectionToVis Targets = new("Targets");
    public static readonly SectionToVis Log = new("Log");
    public static readonly SectionToVis Stats = new("Statistics");
    public static readonly SectionToVis Speed = new("Speed");
    public static readonly SectionToVis Settings = new("Settings");

    private readonly string _name;
    private SectionToVis(string name) => _name = name;

    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s && s == _name ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
