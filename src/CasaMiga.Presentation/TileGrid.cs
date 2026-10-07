using System.Windows;
using System.Windows.Controls;

namespace CasaMiga.Presentation;

/// <summary>
/// Cuadrícula de tarjetas que llena el ancho: tantas columnas como quepan con <see cref="MinItemWidth"/>, todas del mismo ancho.
/// Cada renglón toma la altura de su tarjeta más alta. Los elementos ocultos no ocupan lugar.
/// </summary>
public sealed class TileGrid : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(TileGrid),
        new FrameworkPropertyMetadata(150d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(TileGrid),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Columnas de la última medición (para pruebas y ajustes de la vista).</summary>
    public int Columns { get; private set; } = 1;
    private double itemWidth;
    private double[] rows = [];

    protected override Size MeasureOverride(Size available)
    {
        var width = double.IsInfinity(available.Width) ? MinItemWidth : available.Width;
        Columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
        itemWidth = Math.Max(0, (width - (Columns - 1) * Spacing) / Columns);
        var visible = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
        foreach (var child in InternalChildren.Cast<UIElement>().Where(c => c.Visibility == Visibility.Collapsed)) child.Measure(new Size(0, 0));
        rows = new double[(visible.Length + Columns - 1) / Columns];
        for (var i = 0; i < visible.Length; i++)
        {
            visible[i].Measure(new Size(itemWidth, double.PositiveInfinity));
            rows[i / Columns] = Math.Max(rows[i / Columns], visible[i].DesiredSize.Height);
        }
        var height = rows.Sum() + Math.Max(0, rows.Length - 1) * Spacing;
        return new Size(double.IsInfinity(available.Width) ? Columns * itemWidth : width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var index = 0; var y = 0d;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) { child.Arrange(new Rect()); continue; }
            var row = index / Columns; var column = index % Columns;
            if (column == 0 && row > 0) y += rows[row - 1] + Spacing;
            child.Arrange(new Rect(column * (itemWidth + Spacing), y, itemWidth, row < rows.Length ? rows[row] : 0));
            index++;
        }
        return final;
    }
}
