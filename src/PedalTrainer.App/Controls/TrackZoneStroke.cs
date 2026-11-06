using System.Collections.Generic;
using System.Windows;

namespace PedalTrainer.App.Controls;

/// <summary>Trecho de frenagem já convertido para pixels, pronto para ser desenhado.</summary>
internal readonly record struct TrackZoneStroke(int Index, IReadOnlyList<Point> Points);
