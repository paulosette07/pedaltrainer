using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Medidor circular de precisão (0 a 100). A cor acompanha a faixa da nota, como em jogos.
/// Desenhado à mão para ter o mesmo acabamento do restante da interface.
/// </summary>
public sealed class PrecisionGauge : FrameworkElement
{
    private const double StartAngle = 150;
    private const double SweepAngle = 240;
    private const double StrokeThickness = 11;

    private static readonly Brush TrackBrush = CreateBrush(Color.FromRgb(0x24, 0x2A, 0x33));
    private static readonly Brush ValueBrush = CreateBrush(Color.FromRgb(0x35, 0xE0, 0x8A));
    private static readonly Brush WarningBrush = CreateBrush(Color.FromRgb(0xFF, 0xC2, 0x4B));
    private static readonly Brush DangerBrush = CreateBrush(Color.FromRgb(0xFF, 0x5A, 0x5F));
    private static readonly Brush IdleBrush = CreateBrush(Color.FromRgb(0x3A, 0x42, 0x4D));
    private static readonly Brush ValueText = CreateBrush(Color.FromRgb(0xE9, 0xED, 0xF2));
    private static readonly Brush CaptionText = CreateBrush(Color.FromRgb(0x7C, 0x86, 0x95));
    private static readonly Brush GradeText = CreateBrush(Color.FromRgb(0x9A, 0xA4, 0xB2));

    private double _value;
    private string _caption = "PRECISÃO DA FRENAGEM";
    private string _grade = string.Empty;
    private bool _isActive;

    public PrecisionGauge()
    {
        MinWidth = 190;
        MinHeight = 150;
    }

    /// <summary>Valor atual (0 a 100).</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            InvalidateVisual();
        }
    }

    /// <summary>Texto fixo sob o número.</summary>
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value ?? string.Empty;
            InvalidateVisual();
        }
    }

    /// <summary>Nota em letra (S, A, B...), opcional.</summary>
    public string Grade
    {
        get => _grade;
        set
        {
            _grade = value ?? string.Empty;
            InvalidateVisual();
        }
    }

    /// <summary>Quando falso, o medidor aparece apagado (nenhum desafio em andamento).</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        double radius = Math.Min(width / 2.4, height / 2.05) - (StrokeThickness / 2);

        if (radius <= 10)
        {
            return;
        }

        var center = new Point(width / 2, (height / 2) + (radius * 0.18));
        DrawArc(dc, center, radius, StartAngle, StartAngle + SweepAngle, TrackBrush, StrokeThickness);

        Brush fill = !_isActive ? IdleBrush : _value >= 90 ? ValueBrush : _value >= 70 ? WarningBrush : DangerBrush;
        double sweep = SweepAngle * (_value / 100.0);

        if (sweep > 0.5)
        {
            DrawArc(dc, center, radius, StartAngle, StartAngle + sweep, fill, StrokeThickness);
        }

        string valueText = _isActive ? _value.ToString("0", CultureInfo.InvariantCulture) : "--";
        FormattedText number = CreateText(valueText, ValueText, Math.Max(22, radius * 0.62), FontWeights.Bold, "Consolas");
        dc.DrawText(number, new Point(center.X - (number.Width / 2), center.Y - (number.Height * 0.78)));

        if (_isActive && _grade.Length > 0)
        {
            FormattedText grade = CreateText(_grade, GradeText, Math.Max(12, radius * 0.28), FontWeights.SemiBold, "Segoe UI");
            dc.DrawText(grade, new Point(center.X - (grade.Width / 2), center.Y - (number.Height * 0.1)));
        }

        FormattedText caption = CreateText(_caption, CaptionText, 10.5, FontWeights.SemiBold, "Segoe UI");
        dc.DrawText(caption, new Point(center.X - (caption.Width / 2), height - caption.Height - 2));
    }

    private void DrawArc(DrawingContext dc, Point center, double radius, double startAngle, double endAngle, Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();

        bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;
        Point start = PointOnCircle(center, radius, startAngle);
        Point end = PointOnCircle(center, radius, endAngle);

        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }

    private FormattedText CreateText(string text, Brush brush, double size, FontWeight weight, string fontFamily) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily(fontFamily), FontStyles.Normal, weight, FontStretches.Normal),
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
