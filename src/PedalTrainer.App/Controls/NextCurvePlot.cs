using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Gráfico reduzido da próxima curva: mostra o desenho que o piloto vai encarar na
/// repetição seguinte, com banda de tolerância, pico e duração. Fica sempre visível no
/// treino — inclusive enquanto a repetição atual está sendo executada.
/// Desenho direto, sem subelementos, para acompanhar a taxa do quadro.
/// </summary>
public sealed class NextCurvePlot : FrameworkElement
{
    private const double LeftMargin = 27;
    private const double RightMargin = 12;
    private const double TopMargin = 12;
    private const double BottomMargin = 20;

    private static readonly Brush BackgroundBrush = CreateBrush(Color.FromRgb(0x10, 0x13, 0x17));
    private static readonly Brush GridBrush = CreateBrush(Color.FromArgb(0x3A, 0x2F, 0x36, 0x40));
    private static readonly Brush AxisTextBrush = CreateBrush(Color.FromRgb(0x7C, 0x86, 0x95));
    private static readonly Brush CurveBrush = CreateBrush(Color.FromRgb(0x7A, 0xDF, 0xFF));
    private static readonly Brush EstimatedBrush = CreateBrush(Color.FromRgb(0x9A, 0xA6, 0xB8));
    private static readonly Brush BandBrush = CreateBrush(Color.FromArgb(0x1A, 0x3F, 0xD0, 0xFF));
    private static readonly Brush MutedBrush = CreateBrush(Color.FromRgb(0x5E, 0x66, 0x74));

    private IReadOnlyList<DrillCurvePoint>? _curve;
    private double _tolerancePercent = 5;
    private bool _isEstimated;
    private string _emptyMessage = "Sem próxima curva.";

    public NextCurvePlot()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MinHeight = 90;
    }

    /// <summary>
    /// Define a curva prevista. <paramref name="isEstimated"/> desenha tracejado quando a
    /// altura ainda não é conhecida (alvo sorteado).
    /// </summary>
    public void SetCurve(
        IReadOnlyList<DrillCurvePoint>? curve,
        double tolerancePercent,
        bool isEstimated,
        string emptyMessage)
    {
        bool sameCurve = ReferenceEquals(curve, _curve);
        bool sameMessage = string.Equals(emptyMessage, _emptyMessage, StringComparison.Ordinal);

        if (sameCurve && sameMessage && Math.Abs(tolerancePercent - _tolerancePercent) < 0.001 && isEstimated == _isEstimated)
        {
            return;
        }

        _curve = curve;
        _tolerancePercent = Math.Clamp(tolerancePercent, 0, 40);
        _isEstimated = isEstimated;
        _emptyMessage = emptyMessage ?? string.Empty;

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= LeftMargin + RightMargin || height <= TopMargin + BottomMargin)
        {
            return;
        }

        var plot = new Rect(LeftMargin, TopMargin, width - LeftMargin - RightMargin, height - TopMargin - BottomMargin);

        dc.DrawRoundedRectangle(BackgroundBrush, null, new Rect(0, 0, width, height), 8, 8);
        DrawGrid(dc, plot);

        if (_curve is not { Count: > 1 } curve)
        {
            DrawEmpty(dc, plot);
            return;
        }

        double duration = Math.Max(0.1, curve[^1].TimeSeconds);

        DrawYLabels(dc, plot);
        DrawTimeLabels(dc, plot, duration);
        DrawToleranceBand(dc, plot, curve, duration);
        DrawCurve(dc, plot, curve, duration);
        DrawMarkers(dc, plot, curve, duration);
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(GridBrush, 1);
        pen.Freeze();

        for (var i = 0; i <= 2; i++)
        {
            double y = plot.Top + (plot.Height * i / 2.0);
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
    }

    private void DrawYLabels(DrawingContext dc, Rect plot)
    {
        dc.DrawText(CreateText("100%", AxisTextBrush, 9.5), new Point(4, plot.Top - 6));
        dc.DrawText(CreateText("0%", AxisTextBrush, 9.5), new Point(4, plot.Bottom - 6));
    }

    private void DrawTimeLabels(DrawingContext dc, Rect plot, double durationSeconds)
    {
        dc.DrawText(CreateText("0", AxisTextBrush, 9.5), new Point(plot.Left - 2, plot.Bottom + 5));

        FormattedText end = CreateText(
            string.Create(CultureInfo.CurrentCulture, $"{durationSeconds:0.0} s"),
            AxisTextBrush,
            9.5);

        dc.DrawText(end, new Point(plot.Right - end.Width, plot.Bottom + 5));
    }

    private void DrawToleranceBand(DrawingContext dc, Rect plot, IReadOnlyList<DrillCurvePoint> curve, double duration)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(
                ToPoint(plot, curve[0].TimeSeconds, Math.Min(100, curve[0].Percent + _tolerancePercent), duration),
                true,
                true);

            for (var index = 1; index < curve.Count; index++)
            {
                ctx.LineTo(
                    ToPoint(plot, curve[index].TimeSeconds, Math.Min(100, curve[index].Percent + _tolerancePercent), duration),
                    true,
                    false);
            }

            for (int index = curve.Count - 1; index >= 0; index--)
            {
                ctx.LineTo(
                    ToPoint(plot, curve[index].TimeSeconds, Math.Max(0, curve[index].Percent - _tolerancePercent), duration),
                    true,
                    false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(BandBrush, null, geometry);
    }

    private void DrawCurve(DrawingContext dc, Rect plot, IReadOnlyList<DrillCurvePoint> curve, double duration)
    {
        var pen = new Pen(_isEstimated ? EstimatedBrush : CurveBrush, 2.0)
        {
            LineJoin = PenLineJoin.Round
        };

        if (_isEstimated)
        {
            pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
        }

        pen.Freeze();

        var geometry = new StreamGeometry();

        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(plot, curve[0].TimeSeconds, curve[0].Percent, duration), false, false);

            for (var index = 1; index < curve.Count; index++)
            {
                ctx.LineTo(ToPoint(plot, curve[index].TimeSeconds, curve[index].Percent, duration), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawMarkers(DrawingContext dc, Rect plot, IReadOnlyList<DrillCurvePoint> curve, double duration)
    {
        int peakIndex = PeakIndex(curve);
        DrillCurvePoint peak = curve[peakIndex];
        Brush markerBrush = _isEstimated ? EstimatedBrush : CurveBrush;

        DrawMarker(dc, plot, curve[0], markerBrush, duration);
        DrawMarker(dc, plot, peak, markerBrush, duration);

        FormattedText label = CreateText(
            string.Create(CultureInfo.CurrentCulture, $"{peak.Percent:0}%"),
            markerBrush,
            10.5);

        double peakX = XToPixel(plot, peak.TimeSeconds, duration);
        double peakY = YToPixel(plot, peak.Percent);

        // Perto do topo não há espaço acima do pico: o rótulo desce para não encostar na linha.
        double above = peakY - label.Height - 4;
        double y = above < plot.Top ? peakY + 6 : above;

        double x = peakX + 6;
        if (x + label.Width > plot.Right)
        {
            x = peakX - label.Width - 6;
        }

        if (x >= plot.Left && y + label.Height <= plot.Bottom)
        {
            dc.DrawText(label, new Point(x, y));
        }
    }

    private void DrawMarker(DrawingContext dc, Rect plot, DrillCurvePoint point, Brush brush, double duration)
    {
        Point center = ToPoint(plot, point.TimeSeconds, point.Percent, duration);
        var pen = new Pen(brush, 1.8);
        pen.Freeze();
        dc.DrawEllipse(BackgroundBrush, pen, center, 3.2, 3.2);
    }

    private void DrawEmpty(DrawingContext dc, Rect plot)
    {
        FormattedText text = CreateText(_emptyMessage, MutedBrush, 10.5);
        dc.DrawText(
            text,
            new Point(plot.Left + ((plot.Width - text.Width) / 2), plot.Top + ((plot.Height - text.Height) / 2)));
    }

    private static int PeakIndex(IReadOnlyList<DrillCurvePoint> curve)
    {
        var index = 0;

        for (var i = 1; i < curve.Count; i++)
        {
            if (curve[i].Percent > curve[index].Percent)
            {
                index = i;
            }
        }

        return index;
    }

    private Point ToPoint(Rect plot, double timeSeconds, double percent, double duration) =>
        new(XToPixel(plot, timeSeconds, duration), YToPixel(plot, percent));

    private double XToPixel(Rect plot, double timeSeconds, double duration) =>
        plot.Left + (Math.Clamp(timeSeconds / duration, 0, 1) * plot.Width);

    private double YToPixel(Rect plot, double percent) =>
        plot.Bottom - (Math.Clamp(percent, 0, 100) / 100.0 * plot.Height);

    private FormattedText CreateText(string text, Brush brush, double fontSize) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        fontSize,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
