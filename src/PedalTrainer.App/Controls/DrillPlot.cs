using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Gráfico do exercício: curva-alvo em azul, banda de tolerância e a entrada real do
/// piloto em verde (dentro) ou vermelho (fora). Antes de a repetição começar a curva é
/// desenhada como prévia tracejada, para o piloto saber o que vem pela frente.
/// Desenho direto para manter 60 fps.
/// </summary>
public sealed class DrillPlot : FrameworkElement
{
    private const double LeftMargin = 46;
    private const double RightMargin = 16;
    private const double TopMargin = 14;
    private const double BottomMargin = 28;

    private static readonly Brush BackgroundBrush = CreateBrush(Color.FromRgb(0x10, 0x13, 0x17));
    private static readonly Brush GridBrush = CreateBrush(Color.FromArgb(0x40, 0x2F, 0x36, 0x40));
    private static readonly Brush AxisTextBrush = CreateBrush(Color.FromRgb(0x7C, 0x86, 0x95));
    private static readonly Brush TargetBrush = CreateBrush(Color.FromRgb(0x3F, 0xD0, 0xFF));
    private static readonly Brush PreviewBrush = CreateBrush(Color.FromRgb(0x7A, 0xDF, 0xFF));
    private static readonly Brush ToleranceBrush = CreateBrush(Color.FromArgb(0x1F, 0x3F, 0xD0, 0xFF));
    private static readonly Brush PreviewBandBrush = CreateBrush(Color.FromArgb(0x14, 0x3F, 0xD0, 0xFF));
    private static readonly Brush GoodBrush = CreateBrush(Color.FromRgb(0x35, 0xE0, 0x8A));
    private static readonly Brush BadBrush = CreateBrush(Color.FromRgb(0xFF, 0x5A, 0x5F));
    private static readonly Brush CursorBrush = CreateBrush(Color.FromArgb(0xAA, 0xE9, 0xED, 0xF2));
    private static readonly Brush MutedBrush = CreateBrush(Color.FromRgb(0x5E, 0x66, 0x74));
    private static readonly Brush ChipBrush = CreateBrush(Color.FromArgb(0xC8, 0x1B, 0x20, 0x27));
    private static readonly Brush ChipBorderBrush = CreateBrush(Color.FromArgb(0x66, 0x3F, 0xD0, 0xFF));

    private IReadOnlyList<DrillCurvePoint>? _target;
    private ReadOnlyMemory<DrillCurvePoint> _live;
    private double _tolerancePercent = 5;
    private double _cursorSeconds;
    private double _xMin;
    private double _xMax = 3;
    private double _waitingWindowSeconds = 3;
    private bool _isPreview;

    public DrillPlot()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MinHeight = 200;
    }

    /// <summary>Texto exibido quando não há curva nem repetição em andamento.</summary>
    public string EmptyMessage { get; set; } = "Escolha um programa e clique em Iniciar.";

    /// <summary>Define a curva-alvo e a banda de tolerância da repetição.</summary>
    public void SetTarget(IReadOnlyList<DrillCurvePoint>? target, double tolerancePercent)
    {
        _target = target;
        _tolerancePercent = Math.Clamp(tolerancePercent, 0, 40);

        if (target is { Count: > 1 })
        {
            _xMin = 0;
            _xMax = target[^1].TimeSeconds;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Liga a prévia: a curva aparece tracejada, com pico e duração, para o piloto ver o
    /// desenho antes de começar o movimento.
    /// </summary>
    public void SetPreview(bool isPreview)
    {
        if (_isPreview == isPreview)
        {
            return;
        }

        _isPreview = isPreview;
        InvalidateVisual();
    }

    /// <summary>Define a curva do piloto (tempo relativo ao início da repetição).</summary>
    public void SetLive(ReadOnlyMemory<DrillCurvePoint> live, double cursorSeconds)
    {
        _live = live;
        _cursorSeconds = cursorSeconds;
        InvalidateVisual();
    }

    /// <summary>Janela usada quando a repetição ainda não começou (segundos para trás).</summary>
    public void SetWaiting(double windowSeconds)
    {
        _waitingWindowSeconds = Math.Max(0.5, windowSeconds);
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

        dc.DrawRoundedRectangle(BackgroundBrush, null, new Rect(0, 0, width, height), 10, 10);

        if (_target is { Count: > 1 })
        {
            _xMin = 0;
            _xMax = _target[^1].TimeSeconds;
            DrawGrid(dc, plot);
            DrawAxisLabels(dc, plot);

            if (_isPreview)
            {
                DrawPreviewCurve(dc, plot);
                return;
            }

            DrawToleranceBand(dc, plot);
            DrawTarget(dc, plot);
            DrawLive(dc, plot);
            DrawCursor(dc, plot);
            return;
        }

        _xMin = -_waitingWindowSeconds;
        _xMax = 0;
        DrawGrid(dc, plot);
        DrawAxisLabels(dc, plot);
        DrawLive(dc, plot);

        FormattedText text = CreateText(EmptyMessage, MutedBrush, 12);
        dc.DrawText(text, new Point(plot.Left + ((plot.Width - text.Width) / 2), plot.Top + (plot.Height / 2) - 10));
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(GridBrush, 1);
        pen.Freeze();

        for (var i = 0; i <= 4; i++)
        {
            double y = plot.Top + (plot.Height * i / 4.0);
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        for (var i = 0; i <= 6; i++)
        {
            double x = plot.Left + (plot.Width * i / 6.0);
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    private void DrawAxisLabels(DrawingContext dc, Rect plot)
    {
        for (var i = 0; i <= 4; i++)
        {
            int value = 100 - (i * 25);
            double y = plot.Top + (plot.Height * i / 4.0) - 7;
            dc.DrawText(CreateText($"{value}%", AxisTextBrush, 10.5), new Point(6, y));
        }

        for (var i = 0; i <= 6; i++)
        {
            double time = _xMin + ((_xMax - _xMin) * i / 6.0);
            double x = plot.Left + (plot.Width * i / 6.0) - 10;
            dc.DrawText(
                CreateText(time.ToString("0.0", CultureInfo.InvariantCulture), AxisTextBrush, 10.5),
                new Point(x, plot.Bottom + 6));
        }
    }

    private void DrawToleranceBand(DrawingContext dc, Rect plot) => DrawToleranceBand(dc, plot, ToleranceBrush);

    private void DrawToleranceBand(DrawingContext dc, Rect plot, Brush brush)
    {
        IReadOnlyList<DrillCurvePoint> target = _target!;
        var geometry = new StreamGeometry();

        using (StreamGeometryContext ctx = geometry.Open())
        {
            DrillCurvePoint first = target[0];
            ctx.BeginFigure(ToPoint(plot, first.TimeSeconds, Math.Min(100, first.Percent + _tolerancePercent)), true, true);

            for (var i = 1; i < target.Count; i++)
            {
                ctx.LineTo(ToPoint(plot, target[i].TimeSeconds, Math.Min(100, target[i].Percent + _tolerancePercent)), true, false);
            }

            for (int i = target.Count - 1; i >= 0; i--)
            {
                ctx.LineTo(ToPoint(plot, target[i].TimeSeconds, Math.Max(0, target[i].Percent - _tolerancePercent)), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    private void DrawTarget(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(TargetBrush, 2.2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, BuildCurveGeometry(plot));
    }

    private StreamGeometry BuildCurveGeometry(Rect plot)
    {
        IReadOnlyList<DrillCurvePoint> target = _target!;
        var geometry = new StreamGeometry();

        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(plot, target[0].TimeSeconds, target[0].Percent), false, false);

            for (var i = 1; i < target.Count; i++)
            {
                ctx.LineTo(ToPoint(plot, target[i].TimeSeconds, target[i].Percent), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Curva ainda não começou: desenho tracejado, marcadores de largada e de pico e uma
    /// etiqueta com o pico e a duração, para o piloto saber o que vai fazer.
    /// </summary>
    private void DrawPreviewCurve(DrawingContext dc, Rect plot)
    {
        IReadOnlyList<DrillCurvePoint> target = _target!;

        DrawToleranceBand(dc, plot, PreviewBandBrush);

        var pen = new Pen(PreviewBrush, 2.2)
        {
            LineJoin = PenLineJoin.Round,
            DashStyle = new DashStyle(new double[] { 5, 3.5 }, 0)
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, BuildCurveGeometry(plot));

        int peakIndex = PeakIndex(target);
        DrillCurvePoint peak = target[peakIndex];

        DrawMarker(dc, plot, target[0], PreviewBrush, 4.5);
        DrawMarker(dc, plot, peak, PreviewBrush, 4);

        double peakLabelX = XToPixel(plot, peak.TimeSeconds) + 8;
        double peakLabelY = YToPixel(plot, peak.Percent) - 18;

        if (peakLabelX < plot.Right - 42)
        {
            dc.DrawText(
                CreateText($"{peak.Percent:0}%", PreviewBrush, 11),
                new Point(peakLabelX, Math.Max(plot.Top + 2, peakLabelY)));
        }

        DrawPreviewChip(dc, plot, peak, target[^1].TimeSeconds);
    }

    private void DrawMarker(DrawingContext dc, Rect plot, DrillCurvePoint point, Brush brush, double radius)
    {
        Point center = ToPoint(plot, point.TimeSeconds, point.Percent);
        dc.DrawEllipse(BackgroundBrush, new Pen(brush, 2), center, radius, radius);
    }

    private void DrawPreviewChip(DrawingContext dc, Rect plot, DrillCurvePoint peak, double durationSeconds)
    {
        FormattedText title = CreateText("PRÓXIMA CURVA", PreviewBrush, 11);
        FormattedText detail = CreateText(
            string.Create(CultureInfo.CurrentCulture, $"pico {peak.Percent:0}% · {durationSeconds:0.0} s"),
            AxisTextBrush,
            10.5);

        double width = Math.Max(title.Width, detail.Width) + 20;
        double height = title.Height + detail.Height + 12;
        double left = plot.Left + 12;
        double top = plot.Top + 10;

        var chip = new Rect(left, top, width, height);
        var borderPen = new Pen(ChipBorderBrush, 1);
        borderPen.Freeze();
        dc.DrawRoundedRectangle(ChipBrush, borderPen, chip, 7, 7);

        dc.DrawText(title, new Point(left + 10, top + 5));
        dc.DrawText(detail, new Point(left + 10, top + 6 + title.Height));
    }

    private void DrawLive(DrawingContext dc, Rect plot)
    {
        if (_live.Length < 2)
        {
            return;
        }

        ReadOnlySpan<DrillCurvePoint> live = _live.Span;
        var goodPen = new Pen(GoodBrush, 2.4) { LineJoin = PenLineJoin.Round };
        goodPen.Freeze();
        var badPen = new Pen(BadBrush, 2.4) { LineJoin = PenLineJoin.Round };
        badPen.Freeze();

        var targetIndex = 0;

        for (var i = 1; i < live.Length; i++)
        {
            DrillCurvePoint previous = live[i - 1];
            DrillCurvePoint current = live[i];

            bool inside = _target is null
                || Math.Abs(previous.Percent - SampleTarget(previous.TimeSeconds, ref targetIndex)) <= _tolerancePercent;

            dc.DrawLine(
                inside ? goodPen : badPen,
                ToPoint(plot, previous.TimeSeconds, previous.Percent),
                ToPoint(plot, current.TimeSeconds, current.Percent));
        }
    }

    private void DrawCursor(DrawingContext dc, Rect plot)
    {
        if (_cursorSeconds < _xMin || _cursorSeconds > _xMax)
        {
            return;
        }

        var pen = new Pen(CursorBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) };
        pen.Freeze();

        double x = XToPixel(plot, _cursorSeconds);
        dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
    }

    private static int PeakIndex(IReadOnlyList<DrillCurvePoint> target)
    {
        var index = 0;

        for (var i = 1; i < target.Count; i++)
        {
            if (target[i].Percent > target[index].Percent)
            {
                index = i;
            }
        }

        return index;
    }

    private double SampleTarget(double timeSeconds, ref int index)
    {
        IReadOnlyList<DrillCurvePoint>? target = _target;
        if (target is null || target.Count == 0)
        {
            return 0;
        }

        while (index < target.Count - 2 && target[index + 1].TimeSeconds < timeSeconds)
        {
            index++;
        }

        DrillCurvePoint from = target[Math.Clamp(index, 0, target.Count - 1)];
        DrillCurvePoint to = target[Math.Clamp(index + 1, 0, target.Count - 1)];
        double span = to.TimeSeconds - from.TimeSeconds;

        if (span <= 0)
        {
            return from.Percent;
        }

        double ratio = Math.Clamp((timeSeconds - from.TimeSeconds) / span, 0, 1);
        return from.Percent + ((to.Percent - from.Percent) * ratio);
    }

    private Point ToPoint(Rect plot, double timeSeconds, double percent) =>
        new(XToPixel(plot, timeSeconds), YToPixel(plot, percent));

    private double XToPixel(Rect plot, double timeSeconds)
    {
        double span = Math.Max(0.001, _xMax - _xMin);
        double ratio = (timeSeconds - _xMin) / span;
        return plot.Left + (Math.Clamp(ratio, -0.05, 1.05) * plot.Width);
    }

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
