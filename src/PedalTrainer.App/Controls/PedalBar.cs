using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Barra horizontal de curso do pedal, com marcador da pressão de referência.
/// Usada para freio e acelerador no painel inferior.
/// </summary>
public sealed class PedalBar : FrameworkElement
{
    private const double TrackHeight = 14;
    private const double LabelHeight = 18;
    private const double Spacing = 6;

    private static readonly Brush TrackBrush = CreateBrush(Color.FromRgb(0x22, 0x28, 0x30));
    private static readonly Brush LabelBrush = CreateBrush(Color.FromRgb(0x8A, 0x94, 0xA3));
    private static readonly Brush ValueBrush = CreateBrush(Color.FromRgb(0xE9, 0xED, 0xF2));
    private static readonly Brush ReferenceBrush = CreateBrush(Color.FromRgb(0x3F, 0xD0, 0xFF));
    private static readonly Brush DefaultFillBrush = CreateBrush(Color.FromRgb(0x35, 0xE0, 0x8A));

    private double _value;
    private double _referenceValue = -1;
    private string _label = "PEDAL";
    private Brush _fillBrush = DefaultFillBrush;

    public PedalBar()
    {
        MinHeight = LabelHeight + Spacing + TrackHeight;
    }

    /// <summary>Curso atual do pedal (0 a 100).</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            InvalidateVisual();
        }
    }

    /// <summary>Pressão de referência (0 a 100); use valor negativo para esconder o marcador.</summary>
    public double ReferenceValue
    {
        get => _referenceValue;
        set
        {
            _referenceValue = value;
            InvalidateVisual();
        }
    }

    /// <summary>Rótulo à esquerda.</summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? string.Empty;
            InvalidateVisual();
        }
    }

    /// <summary>Cor do preenchimento.</summary>
    public Brush FillBrush
    {
        get => _fillBrush;
        set
        {
            _fillBrush = value ?? DefaultFillBrush;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        if (width <= 20)
        {
            return;
        }

        FormattedText label = CreateText(_label, LabelBrush, 10.5);
        dc.DrawText(label, new Point(0, 1));

        string valueText = _value.ToString("0", CultureInfo.InvariantCulture) + "%";
        FormattedText value = CreateText(valueText, ValueBrush, 13);
        dc.DrawText(value, new Point(width - value.Width, 0));

        double trackTop = LabelHeight + Spacing;
        var track = new Rect(0, trackTop, width, TrackHeight);
        dc.DrawRoundedRectangle(TrackBrush, null, track, TrackHeight / 2, TrackHeight / 2);

        double fillWidth = width * (_value / 100.0);
        if (fillWidth > 2)
        {
            var fill = new Rect(0, trackTop, fillWidth, TrackHeight);
            dc.DrawRoundedRectangle(_fillBrush, null, fill, TrackHeight / 2, TrackHeight / 2);
        }

        if (_referenceValue >= 0)
        {
            double x = width * (Math.Clamp(_referenceValue, 0, 100) / 100.0);
            x = Math.Clamp(x, 1, width - 1);

            var markerPen = new Pen(ReferenceBrush, 2);
            markerPen.Freeze();
            dc.DrawLine(markerPen, new Point(x, trackTop - 3), new Point(x, trackTop + TrackHeight + 3));

            var triangle = new StreamGeometry();
            using (StreamGeometryContext ctx = triangle.Open())
            {
                ctx.BeginFigure(new Point(x - 4, trackTop - 5), true, true);
                ctx.LineTo(new Point(x + 4, trackTop - 5), true, false);
                ctx.LineTo(new Point(x, trackTop - 1), true, false);
            }

            triangle.Freeze();
            dc.DrawGeometry(ReferenceBrush, null, triangle);
        }
    }

    private FormattedText CreateText(string text, Brush brush, double size) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
        size,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
