using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Netpulse.App.ViewModels;

public sealed class SectionToVis : IValueConverter
{
    public static readonly SectionToVis Dash = new("Dashboard");
    public static readonly SectionToVis Monitor = new("Monitor");
    public static readonly SectionToVis Targets = new("Targets");
    public static readonly SectionToVis Log = new("Log");
    public static readonly SectionToVis Stats = new("Statistics");
    public static readonly SectionToVis Speed = new("Speed");
    public static readonly SectionToVis Settings = new("Settings");
    public static readonly SectionToVis Route = new("Route");
    public static readonly SectionToVis History = new("History");
    public static readonly SectionToVis Loss = new("Loss");
    public static readonly SectionToVis HistCharts = new("metrics");
    public static readonly SectionToVis HistLog = new("log");
    public static readonly SectionToVis HistSpeed = new("speed");

    private readonly string _name;
    private SectionToVis(string name) => _name = name;

    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s && s == _name ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToVis : IValueConverter
{
    public static readonly BoolToVis Yes = new(false);
    public static readonly BoolToVis No = new(true);
    private readonly bool _invert;
    private BoolToVis(bool invert) => _invert = invert;

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var on = value is true;
        if (_invert) on = !on;
        return on ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
