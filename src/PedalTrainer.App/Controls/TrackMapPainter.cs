using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Desenho do mapa da pista: asfalto, zonas de frenagem, linha de chegada e os marcadores
/// do carro e da referência. Recebe tudo já projetado em pixels e não guarda estado.
/// </summary>
internal static class TrackMapPainter
{
    private static readonly Brush PanelBrush = CreateBrush(0xFF, 0x10, 0x13, 0x18);
    private static readonly Brush AsphaltShadowBrush = CreateBrush(0xFF, 0x22, 0x29, 0x33);
    private static readonly Brush AsphaltBrush = CreateBrush(0xFF, 0x3B, 0x45, 0x53);
    // As zonas paradas precisam aparecer sobre o asfalto escuro sem competir com as cores
    // de "próxima" (âmbar) e "em treino" (verde).
    private static readonly Brush ZoneIdleBrush = CreateBrush(0xFF, 0x9E, 0x4C, 0x58);
    private static readonly Brush ZoneActiveBrush = CreateBrush(0xFF, 0x35, 0xE0, 0x8A);
    private static readonly Brush ZoneNextBrush = CreateBrush(0xFF, 0xFF, 0xC2, 0x4B);
    private static readonly Brush StartLineBrush = CreateBrush(0xCC, 0xE9, 0xED, 0xF2);
    private static readonly Brush CarBrush = CreateBrush(0xFF, 0x35, 0xE0, 0x8A);
    private static readonly Brush ReferenceBrush = CreateBrush(0xFF, 0x3F, 0xD0, 0xFF);
    private static readonly Brush MutedBrush = CreateBrush(0xFF, 0x5E, 0x66, 0x74);

    public static void DrawPanel(DrawingContext dc, Size size)
    {
        dc.DrawRoundedRectangle(PanelBrush, null, new Rect(0, 0, size.Width, size.Height), 8, 8);
    }

    public static void DrawTrackLayer(DrawingContext dc, StreamGeometry geometry)
    {
        var shadow = new Pen(AsphaltShadowBrush, 6.6) { LineJoin = PenLineJoin.Round };
        shadow.Freeze();

        var line = new Pen(AsphaltBrush, 3.2) { LineJoin = PenLineJoin.Round };
        line.Freeze();

        dc.DrawGeometry(null, shadow, geometry);
        dc.DrawGeometry(null, line, geometry);
    }

    public static void DrawZones(DrawingContext dc, IReadOnlyList<TrackZoneStroke> strokes, int activeIndex, int nextIndex)
    {
        foreach (TrackZoneStroke stroke in strokes)
        {
            if (stroke.Points.Count < 2)
            {
                continue;
            }

            bool isActive = stroke.Index == activeIndex;
            bool isNext = stroke.Index == nextIndex;

            Brush brush = isActive ? ZoneActiveBrush : isNext ? ZoneNextBrush : ZoneIdleBrush;
            double thickness = isActive ? 4.6 : isNext ? 4.0 : 3.0;

            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();

            var geometry = new StreamGeometry();

            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(stroke.Points[0], false, false);

                for (var index = 1; index < stroke.Points.Count; index++)
                {
                    context.LineTo(stroke.Points[index], true, false);
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    public static void DrawStartLine(DrawingContext dc, TrackMapProjection projection, TrackPath path)
    {
        Point center = projection.ToScreen(path.PointAtFraction(0));
        Point ahead = projection.ToScreen(path.PointAtFraction(0.004));

        double angle = Math.Atan2(ahead.Y - center.Y, ahead.X - center.X) + (Math.PI / 2);
        var pen = new Pen(StartLineBrush, 2.0);
        pen.Freeze();

        Vec(center, angle, 8, out Point from);
        Vec(center, angle, -8, out Point to);
        dc.DrawLine(pen, from, to);
    }

    public static void DrawMarkers(DrawingContext dc, TrackMapProjection projection, TrackPath path, double? carFraction, double? referenceFraction)
    {
        if (referenceFraction is double reference)
        {
            Point point = projection.ToScreen(path.PointAtFraction(reference));
            var ring = new Pen(ReferenceBrush, 1.8);
            ring.Freeze();
            dc.DrawEllipse(null, ring, point, 4.6, 4.6);
        }

        if (carFraction is not double car)
        {
            return;
        }

        Point position = projection.ToScreen(path.PointAtFraction(car));
        Point ahead = projection.ToScreen(path.PointAtFraction(car + 0.004));
        double angle = Math.Atan2(ahead.Y - position.Y, ahead.X - position.X);

        Vec(position, angle, 11, out Point nose);
        Vec(position, angle + 2.5, 6.5, out Point left);
        Vec(position, angle - 2.5, 6.5, out Point right);

        var glow = new Pen(CarBrush, 1.4);
        glow.Freeze();

        var wedge = new StreamGeometry();

        using (StreamGeometryContext context = wedge.Open())
        {
            context.BeginFigure(nose, true, true);
            context.LineTo(left, true, false);
            context.LineTo(right, true, false);
        }

        wedge.Freeze();
        dc.DrawEllipse(PanelBrush, glow, position, 6.2, 6.2);
        dc.DrawGeometry(CarBrush, null, wedge);
    }

    public static void DrawPlaceholder(DrawingContext dc, Size size, string message)
    {
        FormattedText text = CreateText(message, MutedBrush);
        dc.DrawText(text, new Point((size.Width - text.Width) * 0.5, (size.Height - text.Height) * 0.5));
    }

    private static void Vec(Point origin, double angle, double length, out Point target)
    {
        target = new Point(
            origin.X + (Math.Cos(angle) * length),
            origin.Y + (Math.Sin(angle) * length));
    }

    private static FormattedText CreateText(string text, Brush brush) => new(
        text ?? string.Empty,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        11,
        brush,
        1.0);

    private static Brush CreateBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }
}
