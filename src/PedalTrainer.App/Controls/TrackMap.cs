using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Mapa da pista escolhida: traçado em escala, zonas de frenagem da referência (a atual em
/// verde e a próxima em âmbar) e a posição do carro vinda da telemetria. Todo o desenho é
/// feito direto no DrawingContext para poder acompanhar a taxa de quadros da tela.
/// </summary>
public sealed class TrackMap : FrameworkElement
{
    private const double Padding = 16;
    private const double MinimumSide = 48;

    private readonly List<TrackZoneStroke> _zoneStrokes = new();

    private TrackDefinition? _track;
    private IReadOnlyList<BrakingZone>? _zones;
    private double _trackLengthMeters;
    private TrackMapProjection? _projection;
    private StreamGeometry? _trackGeometry;
    private Size _renderedSize;
    private double? _carFraction;
    private double? _referenceFraction;
    private int _activeZoneIndex = -1;
    private int _nextZoneIndex = -1;
    private string _placeholder = "Selecione uma pista para ver o mapa.";

    public TrackMap()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        MinHeight = 150;
    }

    /// <summary>Traçado exibido. Nulo mostra a mensagem de espera.</summary>
    public void SetTrack(TrackDefinition? track)
    {
        if (ReferenceEquals(track, _track))
        {
            return;
        }

        _track = track;
        _zones = null;
        _zoneStrokes.Clear();
        _activeZoneIndex = -1;
        _nextZoneIndex = -1;
        InvalidateProjection();
    }

    /// <summary>Zonas de frenagem da referência e o comprimento usado para posicioná-las.</summary>
    public void SetZones(IReadOnlyList<BrakingZone>? zones, double trackLengthMeters)
    {
        bool sameZones = ReferenceEquals(zones, _zones);
        bool sameLength = Math.Abs(trackLengthMeters - _trackLengthMeters) < 0.5;

        if (sameZones && sameLength)
        {
            return;
        }

        _zones = zones;
        _trackLengthMeters = trackLengthMeters;
        InvalidateProjection();
    }

    /// <summary>Destaca a zona do desafio atual e a próxima da fila.</summary>
    public void SetHighlight(int activeZoneIndex, int nextZoneIndex)
    {
        if (_activeZoneIndex == activeZoneIndex && _nextZoneIndex == nextZoneIndex)
        {
            return;
        }

        _activeZoneIndex = activeZoneIndex;
        _nextZoneIndex = nextZoneIndex;
        InvalidateVisual();
    }

    /// <summary>Posições (fração de volta) do carro e da referência; nulo esconde o marcador.</summary>
    public void SetPositions(double? carFraction, double? referenceFraction)
    {
        bool sameCar = Nullable.Equals(_carFraction, carFraction);
        bool sameReference = Nullable.Equals(_referenceFraction, referenceFraction);

        if (sameCar && sameReference)
        {
            return;
        }

        _carFraction = carFraction;
        _referenceFraction = referenceFraction;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        TrackMapPainter.DrawPanel(dc, size);

        if (_track is null || size.Width < MinimumSide || size.Height < MinimumSide)
        {
            TrackMapPainter.DrawPlaceholder(dc, size, _placeholder);
            return;
        }

        EnsureGeometry(size);

        if (_projection is not { IsValid: true } projection || _trackGeometry is null)
        {
            TrackMapPainter.DrawPlaceholder(dc, size, _placeholder);
            return;
        }

        TrackMapPainter.DrawTrackLayer(dc, _trackGeometry);
        TrackMapPainter.DrawZones(dc, _zoneStrokes, _activeZoneIndex, _nextZoneIndex);
        TrackMapPainter.DrawStartLine(dc, projection, _track.Path);
        TrackMapPainter.DrawMarkers(dc, projection, _track.Path, _carFraction, _referenceFraction);
    }

    private void InvalidateProjection()
    {
        _projection = null;
        _trackGeometry = null;
        _zoneStrokes.Clear();
        InvalidateVisual();
    }

    private void EnsureGeometry(Size size)
    {
        if (_projection is not null && Math.Abs(_renderedSize.Width - size.Width) < 0.5 &&
            Math.Abs(_renderedSize.Height - size.Height) < 0.5)
        {
            return;
        }

        _renderedSize = size;

        if (_track is null)
        {
            return;
        }

        _projection = new TrackMapProjection(_track.Path, size, Padding);
        _trackGeometry = BuildTrackGeometry(_track, _projection);
        BuildZoneStrokes(_track, _projection);
    }

    private static StreamGeometry BuildTrackGeometry(TrackDefinition track, TrackMapProjection projection)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(projection.ToScreen(track.Path[0]), false, true);

            for (var index = 1; index < track.Path.Count; index++)
            {
                context.LineTo(projection.ToScreen(track.Path[index]), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private void BuildZoneStrokes(TrackDefinition track, TrackMapProjection projection)
    {
        _zoneStrokes.Clear();

        if (_zones is null || _trackLengthMeters <= 0)
        {
            return;
        }

        foreach (BrakingZone zone in _zones)
        {
            if (!zone.HasDistance)
            {
                continue;
            }

            double start = Math.Clamp(zone.StartDistanceMeters / _trackLengthMeters, 0, 0.999);
            double end = Math.Clamp(zone.EndDistanceMeters / _trackLengthMeters, 0, 1);
            double span = end - start;

            if (span <= 0)
            {
                continue;
            }

            int steps = Math.Clamp((int)Math.Round(span * track.Path.Count), 2, 240);
            var points = new List<Point>(steps + 1);

            for (var step = 0; step <= steps; step++)
            {
                double fraction = start + (span * step / steps);
                points.Add(projection.ToScreen(track.Path.PointAtFraction(fraction)));
            }

            _zoneStrokes.Add(new TrackZoneStroke(zone.Index, points));
        }
    }
}
