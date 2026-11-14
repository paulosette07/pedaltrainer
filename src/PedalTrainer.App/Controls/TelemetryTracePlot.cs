using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Telemetry.Trace;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Traço da volta por distância: freio em vermelho, acelerador em verde e os melhores trechos
/// marcados no topo. O eixo X é a pista (não o relógio), então o mesmo ponto do desenho é o
/// mesmo lugar da pista em qualquer volta.
/// </summary>
public sealed class TelemetryTracePlot : FrameworkElement
{
    private const double LeftMargin = 42;
    private const double RightMargin = 28;
    private const double TopMargin = 26;
    private const double BottomMargin = 26;

    private static readonly Brush BackgroundBrush = CreateBrush(Color.FromRgb(0x10, 0x13, 0x17));
    private static readonly Brush GridBrush = CreateBrush(Color.FromArgb(0x40, 0x2F, 0x36, 0x40));
    private static readonly Brush AxisTextBrush = CreateBrush(Color.FromRgb(0x7C, 0x86, 0x95));
    private static readonly Brush MutedBrush = CreateBrush(Color.FromRgb(0x5E, 0x66, 0x74));
    private static readonly Brush BrakeBrush = CreateBrush(Color.FromRgb(0xFF, 0x5A, 0x5F));
    private static readonly Brush ThrottleBrush = CreateBrush(Color.FromRgb(0x35, 0xE0, 0x8A));
    private static readonly Brush CursorBrush = CreateBrush(Color.FromArgb(0xCC, 0xE9, 0xED, 0xF2));
    private static readonly Brush BestSectorBrush = CreateBrush(Color.FromArgb(0x33, 0x3F, 0xD0, 0xFF));
    private static readonly Brush BeatenSectorBrush = CreateBrush(Color.FromArgb(0x8C, 0x35, 0xE0, 0x8A));
    private static readonly Brush BrakeFillBrush = CreateBrush(Color.FromArgb(0x52, 0xFF, 0x5A, 0x5F));
    private static readonly Brush ThrottleFillBrush = CreateBrush(Color.FromArgb(0x24, 0x35, 0xE0, 0x8A));
    private static readonly Brush LegendBackBrush = CreateBrush(Color.FromArgb(0xB4, 0x10, 0x13, 0x17));

    private IReadOnlyList<TelemetryTraceSample>? _samples;
    private IReadOnlyList<double>? _bestSectors;
    private IReadOnlyList<bool>? _beatenSectors;
    private double _trackLengthMeters;
    private double _positionPercent;
    private double _backgroundOpacity = 1;
    private Brush _backdrop = BackgroundBrush;

    /// <summary>
    /// Opacidade do fundo do próprio gráfico. Na janela destacada o painel pode ficar
    /// translúcido; o gráfico acompanha para não virar um bloco sólido sobre o jogo.
    /// </summary>
    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            double clamped = Math.Clamp(value, 0, 1);

            if (Math.Abs(clamped - _backgroundOpacity) < 0.002)
            {
                return;
            }

            _backgroundOpacity = clamped;
            _backdrop = clamped >= 0.999
                ? BackgroundBrush
                : CreateBrush(Color.FromArgb(
                    (byte)Math.Round(clamped * 255),
                    0x10,
                    0x13,
                    0x17));

            InvalidateVisual();
        }
    }

    public TelemetryTracePlot()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MinHeight = 190;
    }

    /// <summary>Texto exibido enquanto o jogo não publica posição na volta.</summary>
    public string EmptyMessage { get; set; } = "O jogo precisa publicar a posição na volta para desenhar o traço.";

    /// <summary>
    /// Define o traço da volta em andamento, o comprimento da pista e onde o carro está.
    /// <paramref name="trackLengthMeters"/> zero desenha o eixo em porcentagem da volta.
    /// </summary>
    public void SetTrace(IReadOnlyList<TelemetryTraceSample>? samples, double trackLengthMeters, double positionPercent)
    {
        _samples = samples;
        _trackLengthMeters = trackLengthMeters > 0 ? trackLengthMeters : 0;
        _positionPercent = Math.Clamp(positionPercent, 0, 1);
        InvalidateVisual();
    }

    /// <summary>
    /// Marca os melhores trechos: <paramref name="beaten"/> indica os setores que a última
    /// volta melhorou (desenhados em verde), os demais melhores ficam em azul.
    /// </summary>
    public void SetBestSectors(IReadOnlyList<double>? bestSectors, IReadOnlyList<bool>? beaten)
    {
        bool same = ReferenceEquals(bestSectors, _bestSectors) && ReferenceEquals(beaten, _beatenSectors);

        if (same)
        {
            return;
        }

        _bestSectors = bestSectors;
        _beatenSectors = beaten;
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

        var plot = new Rect(
            LeftMargin,
            TopMargin,
            width - LeftMargin - RightMargin,
            height - TopMargin - BottomMargin);

        dc.DrawRoundedRectangle(_backdrop, null, new Rect(0, 0, width, height), 10, 10);

        DrawGrid(dc, plot);
        DrawSectors(dc, plot);

        if (_samples is not { Count: > 1 })
        {
            DrawCenteredMessage(dc, plot);
            DrawAxisLabels(dc, plot);
            return;
        }

        // Acelerador primeiro: o freio é a informação que o piloto persegue, então fica por cima.
        DrawPedal(dc, plot, static sample => sample.ThrottlePercent, ThrottleBrush, ThrottleFillBrush, 1.8);
        DrawPedal(dc, plot, static sample => sample.BrakePercent, BrakeBrush, BrakeFillBrush, 2.0);
        DrawCursor(dc, plot);
        DrawAxisLabels(dc, plot);
        DrawLegend(dc, plot);
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(GridBrush, 1);
        pen.Freeze();

        // Linhas de 0, 25, 50, 75 e 100% do pedal.
        for (int line = 0; line <= 4; line++)
        {
            double y = plot.Top + (plot.Height * line / 4.0);
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        int divisions = 8;

        for (int line = 0; line <= divisions; line++)
        {
            double x = plot.Left + (plot.Width * line / divisions);
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }
    }

    private void DrawSectors(DrawingContext dc, Rect plot)
    {
        IReadOnlyList<double>? best = _bestSectors;

        if (best is null || best.Count == 0)
        {
            return;
        }

        int count = best.Count;
        double sectorWidth = plot.Width / count;
        const double barHeight = 4;

        for (int sector = 0; sector < count; sector++)
        {
            if (best[sector] <= 0)
            {
                continue;
            }

            bool beaten = _beatenSectors is { } mask && sector < mask.Count && mask[sector];
            var bar = new Rect(plot.Left + (sector * sectorWidth) + 0.5, plot.Top - 8, Math.Max(1, sectorWidth - 1), barHeight);

            dc.DrawRoundedRectangle(beaten ? BeatenSectorBrush : BestSectorBrush, null, bar, 2, 2);
        }
    }

    /// <summary>
    /// Desenha o pedal como área preenchida até a base, com a linha por cima: o pé que fica
    /// parado em 100% aparece como faixa cheia, e não como um risco fino que some no meio do
    /// resto do traçado.
    /// </summary>
    private void DrawPedal(
        DrawingContext dc,
        Rect plot,
        Func<TelemetryTraceSample, double> selector,
        Brush stroke,
        Brush fill,
        double thickness)
    {
        IReadOnlyList<TelemetryTraceSample> samples = _samples!;

        var pen = new Pen(stroke, thickness) { LineJoin = PenLineJoin.Round };
        pen.Freeze();

        IReadOnlyList<Point> points = BuildPedalPoints(plot, selector);

        if (points.Count < 2)
        {
            return;
        }

        var area = new StreamGeometry();
        var line = new StreamGeometry();

        using (StreamGeometryContext ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, plot.Bottom), true, true);

            for (int index = 0; index < points.Count; index++)
            {
                ctx.LineTo(points[index], true, false);
            }

            ctx.LineTo(new Point(points[^1].X, plot.Bottom), true, false);
        }

        using (StreamGeometryContext ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);

            for (int index = 1; index < points.Count; index++)
            {
                ctx.LineTo(points[index], true, false);
            }
        }

        area.Freeze();
        line.Freeze();

        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, pen, line);
    }

    /// <summary>
    /// Converte as amostras em pontos do desenho. Quando há mais amostras do que pixels de
    /// largura (o caso da janela destacada), cada coluna recebe a média dos seus quadros: sem
    /// isso o gráfico estreito vira um pente de riscos verticais ilegível.
    /// </summary>
    private IReadOnlyList<Point> BuildPedalPoints(Rect plot, Func<TelemetryTraceSample, double> selector)
    {
        IReadOnlyList<TelemetryTraceSample> samples = _samples!;

        if (samples.Count <= plot.Width)
        {
            var direct = new Point[samples.Count];

            for (int index = 0; index < samples.Count; index++)
            {
                direct[index] = ToPoint(plot, samples[index].LapPercent, selector(samples[index]));
            }

            return direct;
        }

        int columns = Math.Max(2, (int)Math.Ceiling(plot.Width));
        var sums = new double[columns];
        var counts = new int[columns];

        for (int index = 0; index < samples.Count; index++)
        {
            int column = Math.Clamp((int)(samples[index].LapPercent * plot.Width), 0, columns - 1);
            sums[column] += Math.Clamp(selector(samples[index]), 0, 100);
            counts[column]++;
        }

        var points = new List<Point>(columns);

        for (int column = 0; column < columns; column++)
        {
            if (counts[column] == 0)
            {
                continue;
            }

            double average = sums[column] / counts[column];
            points.Add(new Point(
                plot.Left + column + 0.5,
                plot.Bottom - (average / 100.0 * plot.Height)));
        }

        return points;
    }

    private void DrawCursor(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(CursorBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) };
        pen.Freeze();

        double x = plot.Left + (_positionPercent * plot.Width);
        dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        dc.DrawEllipse(CursorBrush, null, new Point(x, plot.Top), 3, 3);
    }

    private void DrawAxisLabels(DrawingContext dc, Rect plot)
    {
        for (int line = 0; line <= 4; line++)
        {
            int value = 100 - (line * 25);
            double y = plot.Top + (plot.Height * line / 4.0) - 7;
            DrawText(dc, value.ToString(CultureInfo.CurrentCulture) + "%", new Point(8, y), AxisTextBrush, 10.5);
        }

        int divisions = 8;
        FormattedText widest = CreateText(FormatDistanceLabel(1), AxisTextBrush, 10.5);
        double tickSpacing = plot.Width / divisions;

        // Em janela estreita os rótulos de distância encostariam um no outro: desenha de
        // tantos em tantos até o espaçamento caber.
        int step = 1;

        while (step < divisions && tickSpacing * step < widest.Width + 16)
        {
            step++;
        }

        // A linha de chegada é a marca mais importante do eixo: ela reserva o canto direito
        // antes de todo mundo, para continuar aparecendo na janela estreita.
        FormattedText finish = widest;
        double finishX = Math.Max(plot.Left, plot.Right - finish.Width);
        double limit = finishX - 6;
        double lastRight = double.NegativeInfinity;

        for (int line = 0; line < divisions; line += step)
        {
            DrawDistanceLabel(dc, plot, (double)line / divisions, limit, ref lastRight);
        }

        dc.DrawText(finish, new Point(finishX, plot.Bottom + 5));
    }

    /// <summary>
    /// Um rótulo de distância centrado na marca, encostado na borda quando necessário e
    /// omitido quando invadiria o rótulo anterior.
    /// </summary>
    private void DrawDistanceLabel(
        DrawingContext dc,
        Rect plot,
        double lapPercent,
        double limit,
        ref double lastRight)
    {
        FormattedText label = CreateText(FormatDistanceLabel(lapPercent), AxisTextBrush, 10.5);
        double x = Math.Max(
            plot.Left,
            Math.Min(plot.Left + (plot.Width * lapPercent) - (label.Width / 2), plot.Right - label.Width));

        if (x < lastRight + 10 || x + label.Width > limit)
        {
            return;
        }

        dc.DrawText(label, new Point(x, plot.Bottom + 5));
        lastRight = x + label.Width;
    }

    /// <summary>Legenda compacta no canto do gráfico, com um quadradinho da cor de cada pedal.</summary>
    private void DrawLegend(DrawingContext dc, Rect plot)
    {
        FormattedText brake = CreateText("freio", AxisTextBrush, 10);
        FormattedText throttle = CreateText("acelerador", AxisTextBrush, 10);

        const double swatchWidth = 8;
        const double swatchGap = 5;
        const double itemGap = 12;

        double total = (swatchWidth + swatchGap + brake.Width) + itemGap + (swatchWidth + swatchGap + throttle.Width);
        double x = plot.Right - total;
        double y = 4;

        dc.DrawRoundedRectangle(
            LegendBackBrush,
            null,
            new Rect(x - 7, y - 2, total + 14, brake.Height + 6),
            4,
            4);

        dc.DrawRectangle(BrakeBrush, null, new Rect(x, y + 2.5, swatchWidth, 3));
        dc.DrawText(brake, new Point(x + swatchWidth + swatchGap, y));

        x += swatchWidth + swatchGap + brake.Width + itemGap;

        dc.DrawRectangle(ThrottleBrush, null, new Rect(x, y + 2.5, swatchWidth, 3));
        dc.DrawText(throttle, new Point(x + swatchWidth + swatchGap, y));
    }

    /// <summary>Rótulo do eixo: metros quando o comprimento da pista é conhecido, senão %.</summary>
    private string FormatDistanceLabel(double lapPercent) =>
        _trackLengthMeters > 0
            ? (lapPercent * _trackLengthMeters).ToString("0", CultureInfo.CurrentCulture) + " m"
            : (lapPercent * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    private void DrawCenteredMessage(DrawingContext dc, Rect plot)
    {
        FormattedText text = CreateText(EmptyMessage, MutedBrush, 12);
        double x = plot.Left + ((plot.Width - text.Width) / 2);
        double y = plot.Top + ((plot.Height - text.Height) / 2);
        dc.DrawText(text, new Point(Math.Max(plot.Left + 6, x), y));
    }

    private static Point ToPoint(Rect plot, double lapPercent, double pedalPercent) =>
        new(
            plot.Left + (Math.Clamp(lapPercent, 0, 1) * plot.Width),
            plot.Bottom - (Math.Clamp(pedalPercent, 0, 100) / 100.0 * plot.Height));

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
