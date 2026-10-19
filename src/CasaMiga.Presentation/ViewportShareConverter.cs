using System.Globalization;
using System.Windows.Data;

namespace CasaMiga.Presentation;

/// <summary>Sizes a list inside a scrolling form as a bounded share of the visible viewport.</summary>
public sealed class ViewportShareConverter : IValueConverter
{
    public double Share { get; set; } = 0.5;
    public double Min { get; set; } = 200;
    public double Max { get; set; } = double.PositiveInfinity;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double viewport && viewport > 0 && !double.IsInfinity(viewport)
            ? Math.Clamp(viewport * Share, Min, Math.Max(Min, Max)) : Min;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
