using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Models;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Gráfico principal: linha fantasma da referência (azul), entrada do piloto (verde/vermelho
/// conforme o erro) e banda de tolerância. Desenho direto com DrawingContext para manter 60 fps.
/// </summary>
public sealed class TracePlot : FrameworkElement
{
    private const double LeftMargin = 46;
    private const double RightMargin = 18;
    private const double TopMargin = 16;
    private const double BottomMargin = 30;

    private static readonly Brush BackgroundBrush = CreateBrush(Color.FromRgb(0x10, 0x13, 0x17));
    private static readonly Brush GridBrush = CreateBrush(Color.FromArgb(0x40, 0x2F, 0x36, 0x40));
    private static readonly Brush AxisTextBrush = CreateBrush(Color.FromRgb(0x7C, 0x86, 0x95));
    private static readonly Brush GhostBrush = CreateBrush(Color.FromRgb(0x3F, 0xD0, 0xFF));
    private static readonly Brush GhostSoftBrush = CreateBrush(Color.FromArgb(0x33, 0x3F, 0xD0, 0xFF));
    private static readonly Brush ToleranceBrush = CreateBrush(Color.FromArgb(0x1F, 0x3F, 0xD0, 0xFF));
    private static readonly Brush GoodBrush = CreateBrush(Color.FromRgb(0x35, 0xE0, 0x8A));
    private static readonly Brush BadBrush = CreateBrush(Color.FromRgb(0xFF, 0x5A, 0x5F));
    private static readonly Brush ThrottleBrush = CreateBrush(Color.FromArgb(0x99, 0x7C, 0x9C, 0xFF));
    private static readonly Brush CursorBrush = CreateBrush(Color.FromArgb(0xAA, 0xE9, 0xED, 0xF2));
    private static readonly Brush MutedBrush = CreateBrush(Color.FromRgb(0x5E, 0x66, 0x74));

    private IReadOnlyList<TraceSample>? _ghost;
    private ReadOnlyMemory<TraceSample> _live;
    private double _cursorSeconds;
    private double _tolerancePercent = 8;
    private double _xMin = -1;
    private double _xMax = 3;

    public TracePlot()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MinHeight = 220;
    }

    /// <summary>Exibe também a curva do acelerador.</summary>
    public bool ShowThrottle { get; set; } = true;

    /// <summary>Texto exibido quando não há desafio ativo.</summary>
    public string EmptyMessage { get; set; } = "Carregue uma volta de referência e comece a frear.";

    /// <summary>Define a curva fantasma e a banda de tolerância.</summary>
    public void SetGhost(IReadOnlyList<TraceSample>? ghost, double tolerancePercent)
    {
        _ghost = ghost;
        _tolerancePercent = Math.Clamp(tolerancePercent, 0, 40);

        if (ghost is { Count: > 1 })
        {
            _xMin = ghost[0].TimeSeconds;
            _xMax = ghost[^1].TimeSeconds;
        }
        else
        {
            _xMin = -1;
            _xMax = 3;
        }

        InvalidateVisual();
    }

    /// <summary>Define a curva do piloto e a posição do cursor.</summary>
    public void SetLive(ReadOnlyMemory<TraceSample> live, double cursorSeconds)
    {
        _live = live;
        _cursorSeconds = cursorSeconds;
        InvalidateVisual();
    }

    /// <summary>Define manualmente a janela de tempo (usada quando não há desafio ativo).</summary>
    public void SetTimeRange(double minSeconds, double maxSeconds)
    {
        if (maxSeconds - minSeconds < 0.2)
        {
            maxSeconds = minSeconds + 0.2;
        }

        _xMin = minSeconds;
        _xMax = maxSeconds;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= LeftMargin + RightMargin || height <= TopMargin + BottomMargin)
        {
            return;
        }

        var plot = new Rect(LeftMargin, TopMargin, width - LeftMargin - RightMargin, height - TopMargin - BottomMargin);

        dc.DrawRoundedRectangle(BackgroundBrush, null, new Rect(0, 0, width, height), 10, 10);
        DrawGrid(dc, plot);
        DrawAxisLabels(dc, plot);

        if (_ghost is { Count: > 1 })
        {
            DrawToleranceBand(dc, plot);
            DrawGhost(dc, plot);
        }

        DrawLive(dc, plot);
        DrawCursor(dc, plot);

        if (_live.Length == 0)
        {
            DrawEmptyMessage(dc, plot);
        }
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        var gridPen = new Pen(GridBrush, 1);
        gridPen.Freeze();

        for (var i = 0; i <= 4; i++)
        {
            double y = plot.Top + (plot.Height * i / 4.0);
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        int divisions = 6;
        for (var i = 0; i <= divisions; i++)
        {
            double x = plot.Left + (plot.Width * i / divisions);
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    private void DrawAxisLabels(DrawingContext dc, Rect plot)
    {
        for (var i = 0; i <= 4; i++)
        {
            int value = 100 - (i * 25);
            double y = plot.Top + (plot.Height * i / 4.0) - 7;
            DrawText(dc, $"{value}%", new Point(6, y), AxisTextBrush, 10.5);
        }

        int divisions = 6;
        for (var i = 0; i <= divisions; i++)
        {
            double time = _xMin + ((_xMax - _xMin) * i / divisions);
            double x = plot.Left + (plot.Width * i / divisions) - 12;
            DrawText(dc, time.ToString("0.0", CultureInfo.InvariantCulture), new Point(x, plot.Bottom + 6), AxisTextBrush, 10.5);
        }
    }

    private void DrawToleranceBand(DrawingContext dc, Rect plot)
    {
        IReadOnlyList<TraceSample> ghost = _ghost!;
        var geometry = new StreamGeometry();

        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(plot, ghost[0].TimeSeconds, Math.Min(100, ghost[0].BrakePercent + _tolerancePercent)), true, true);

            for (var i = 1; i < ghost.Count; i++)
            {
                ctx.LineTo(ToPoint(plot, ghost[i].TimeSeconds, Math.Min(100, ghost[i].BrakePercent + _tolerancePercent)), true, false);
            }

            for (int i = ghost.Count - 1; i >= 0; i--)
            {
                ctx.LineTo(ToPoint(plot, ghost[i].TimeSeconds, Math.Max(0, ghost[i].BrakePercent - _tolerancePercent)), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(ToleranceBrush, null, geometry);
    }

    private void DrawGhost(DrawingContext dc, Rect plot)
    {
        IReadOnlyList<TraceSample> ghost = _ghost!;
        var brakePen = new Pen(GhostBrush, 2.2) { LineJoin = PenLineJoin.Round };
        brakePen.Freeze();

        var brakeGeometry = new StreamGeometry();
        using (StreamGeometryContext ctx = brakeGeometry.Open())
        {
            ctx.BeginFigure(ToPoint(plot, ghost[0].TimeSeconds, ghost[0].BrakePercent), false, false);
            for (var i = 1; i < ghost.Count; i++)
            {
                ctx.LineTo(ToPoint(plot, ghost[i].TimeSeconds, ghost[i].BrakePercent), true, false);
            }
        }

        brakeGeometry.Freeze();
        dc.DrawGeometry(null, brakePen, brakeGeometry);

        if (ShowThrottle)
        {
            DrawPolyline(dc, plot, ghost, static s => s.ThrottlePercent, GhostSoftBrush, 1.2, dashed: true);
        }
    }

    private void DrawLive(DrawingContext dc, Rect plot)
    {
        if (_live.Length < 2)
        {
            return;
        }

        ReadOnlySpan<TraceSample> live = _live.Span;
        var goodPen = new Pen(GoodBrush, 2.4) { LineJoin = PenLineJoin.Round };
        goodPen.Freeze();
        var badPen = new Pen(BadBrush, 2.4) { LineJoin = PenLineJoin.Round };
        badPen.Freeze();

        var ghostIndex = 0;

        for (var i = 1; i < live.Length; i++)
        {
            TraceSample previous = live[i - 1];
            TraceSample current = live[i];

            if (current.TimeSeconds < _xMin || previous.TimeSeconds > _xMax)
            {
                continue;
            }

            double referenceBrake = SampleGhostBrake(previous.TimeSeconds, ref ghostIndex);
            bool inside = _ghost is null || Math.Abs(previous.BrakePercent - referenceBrake) <= _tolerancePercent;

            dc.DrawLine(
                inside ? goodPen : badPen,
                ToPoint(plot, previous.TimeSeconds, previous.BrakePercent),
                ToPoint(plot, current.TimeSeconds, current.BrakePercent));
        }

        if (ShowThrottle)
        {
            var throttlePen = new Pen(ThrottleBrush, 1.4) { DashStyle = new DashStyle(new double[] { 2, 3 }, 0) };
            throttlePen.Freeze();

            for (var i = 1; i < live.Length; i++)
            {
                dc.DrawLine(
                    throttlePen,
                    ToPoint(plot, live[i - 1].TimeSeconds, live[i - 1].ThrottlePercent),
                    ToPoint(plot, live[i].TimeSeconds, live[i].ThrottlePercent));
            }
        }
    }

    private void DrawCursor(DrawingContext dc, Rect plot)
    {
        if (_cursorSeconds < _xMin || _cursorSeconds > _xMax)
        {
            return;
        }

        if (_live.Length > 0)
        {
            TraceSample last = _live.Span[_live.Length - 1];
            Point point = ToPoint(plot, last.TimeSeconds, last.BrakePercent);
            dc.DrawEllipse(CursorBrush, null, point, 3.5, 3.5);
        }

        var pen = new Pen(CursorBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) };
        pen.Freeze();
        double x = XToPixel(plot, _cursorSeconds);
        dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
    }

    private void DrawEmptyMessage(DrawingContext dc, Rect plot)
    {
        FormattedText text = CreateText(EmptyMessage, MutedBrush, 12);
        dc.DrawText(text, new Point(plot.Left + ((plot.Width - text.Width) / 2), plot.Top + (plot.Height / 2) - 10));
    }

    private void DrawPolyline(
        DrawingContext dc,
        Rect plot,
        IReadOnlyList<TraceSample> samples,
        Func<TraceSample, double> selector,
        Brush brush,
        double thickness,
        bool dashed)
    {
        var pen = new Pen(brush, thickness);
        if (dashed)
        {
            pen.DashStyle = new DashStyle(new double[] { 2, 3 }, 0);
        }

        pen.Freeze();

        for (var i = 1; i < samples.Count; i++)
        {
            dc.DrawLine(
                pen,
                ToPoint(plot, samples[i - 1].TimeSeconds, selector(samples[i - 1])),
                ToPoint(plot, samples[i].TimeSeconds, selector(samples[i])));
        }
    }

    private double SampleGhostBrake(double timeSeconds, ref int index)
    {
        IReadOnlyList<TraceSample>? ghost = _ghost;
        if (ghost is null || ghost.Count == 0)
        {
            return 0;
        }

        while (index < ghost.Count - 2 && ghost[index + 1].TimeSeconds < timeSeconds)
        {
            index++;
        }

        TraceSample from = ghost[Math.Clamp(index, 0, ghost.Count - 1)];
        TraceSample to = ghost[Math.Clamp(index + 1, 0, ghost.Count - 1)];
        double span = to.TimeSeconds - from.TimeSeconds;

        if (span <= 0)
        {
            return from.BrakePercent;
        }

        double ratio = Math.Clamp((timeSeconds - from.TimeSeconds) / span, 0, 1);
        return from.BrakePercent + ((to.BrakePercent - from.BrakePercent) * ratio);
    }

    private Point ToPoint(Rect plot, double timeSeconds, double percent) =>
        new(XToPixel(plot, timeSeconds), YToPixel(plot, percent));

    private double XToPixel(Rect plot, double timeSeconds)
    {
        double span = Math.Max(0.001, _xMax - _xMin);
        double ratio = (_xMax - _xMin) <= 0 ? 0 : (timeSeconds - _xMin) / span;
        return plot.Left + (Math.Clamp(ratio, -0.05, 1.05) * plot.Width);
    }

    private double YToPixel(Rect plot, double percent) =>
        plot.Bottom - (Math.Clamp(percent, 0, 100) / 100.0 * plot.Height);

    private void DrawText(DrawingContext dc, string text, Point origin, Brush brush, double fontSize) =>
        dc.DrawText(CreateText(text, brush, fontSize), origin);

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
