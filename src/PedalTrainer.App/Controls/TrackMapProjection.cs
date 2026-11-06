using System;
using System.Windows;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.App.Controls;

/// <summary>
/// Converte os pontos do traçado (metros, Y para norte) em pixels da tela (Y para baixo),
/// mantendo a proporção da pista e centralizando o desenho na área disponível.
/// </summary>
internal sealed class TrackMapProjection
{
    private readonly double _minX;
    private readonly double _minY;
    private readonly double _scale;
    private readonly double _offsetX;
    private readonly double _offsetY;
    private readonly double _height;

    public TrackMapProjection(TrackPath path, Size size, double padding)
    {
        ArgumentNullException.ThrowIfNull(path);

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;

        foreach (TrackPoint point in path.Points)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        double spanX = Math.Max(1, maxX - minX);
        double spanY = Math.Max(1, maxY - minY);
        double availableWidth = Math.Max(1, size.Width - (2 * padding));
        double availableHeight = Math.Max(1, size.Height - (2 * padding));

        _scale = Math.Min(availableWidth / spanX, availableHeight / spanY);
        _minX = minX;
        _minY = minY;
        _height = size.Height;

        double drawnWidth = spanX * _scale;
        double drawnHeight = spanY * _scale;
        _offsetX = (size.Width - drawnWidth) * 0.5;
        _offsetY = (size.Height - drawnHeight) * 0.5;

        IsValid = _scale > 0 && double.IsFinite(_scale);
    }

    public bool IsValid { get; }

    public double Scale => _scale;

    public Point ToScreen(TrackPoint point) => new(
        _offsetX + ((point.X - _minX) * _scale),
        _height - (_offsetY + ((point.Y - _minY) * _scale)));
}
