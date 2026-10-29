using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Curva-alvo de uma repetição: a sequência de segmentos resolvida para um alvo concreto,
/// com o percentual esperado em cada instante e o trecho vigente.
/// </summary>
public sealed class DrillProfile
{
    private readonly List<Segment> _segments = new();

    /// <summary>Constrói o perfil da etapa para um alvo (fixo ou sorteado).</summary>
    public DrillProfile(DrillStep step, double targetPercent)
    {
        ArgumentNullException.ThrowIfNull(step);

        TargetPercent = Math.Clamp(targetPercent, 0, 100);
        Step = step;

        double clock = 0;
        double value = 0;

        foreach (DrillSegment segment in step.Segments)
        {
            if (segment.Seconds <= 0)
            {
                continue;
            }

            double to = segment.Kind == DrillSegmentKind.Hold
                ? value
                : Math.Clamp(segment.ToPercent ?? TargetPercent, 0, 100);

            var resolved = new Segment(
                kind: segment.Kind,
                startSeconds: clock,
                endSeconds: clock + segment.Seconds,
                fromPercent: value,
                toPercent: to);

            _segments.Add(resolved);
            clock = resolved.EndSeconds;
            value = to;
            PeakPercent = Math.Max(PeakPercent, value);
        }

        if (_segments.Count == 0)
        {
            throw new ArgumentException("A etapa não tem segmentos válidos.", nameof(step));
        }

        DurationSeconds = clock;
        FinalPercent = value;
    }

    /// <summary>Etapa que originou o perfil.</summary>
    public DrillStep Step { get; }

    /// <summary>Alvo usado nesta repetição.</summary>
    public double TargetPercent { get; }

    /// <summary>Duração total da repetição, em segundos.</summary>
    public double DurationSeconds { get; }

    /// <summary>Maior percentual tocado pela curva.</summary>
    public double PeakPercent { get; }

    /// <summary>Percentual ao final da repetição (normalmente 0).</summary>
    public double FinalPercent { get; }

    /// <summary>Quantidade de trechos resolvidos.</summary>
    public int SegmentCount => _segments.Count;

    /// <summary>Percentual esperado no instante informado.</summary>
    public double TargetAt(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
        {
            return _segments[0].FromPercent;
        }

        if (elapsedSeconds >= DurationSeconds)
        {
            return FinalPercent;
        }

        return SegmentAt(elapsedSeconds).PercentAt(elapsedSeconds);
    }

    /// <summary>Índice do trecho vigente (0 é o primeiro).</summary>
    public int SegmentIndexAt(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
        {
            return 0;
        }

        for (var i = 0; i < _segments.Count; i++)
        {
            if (elapsedSeconds < _segments[i].EndSeconds)
            {
                return i;
            }
        }

        return _segments.Count - 1;
    }

    /// <summary>Forma do trecho vigente no instante informado.</summary>
    public DrillSegmentKind KindAt(double elapsedSeconds) => SegmentAt(elapsedSeconds).Kind;

    /// <summary>Forma do trecho informado.</summary>
    public DrillSegmentKind SegmentKind(int index) =>
        _segments[Math.Clamp(index, 0, _segments.Count - 1)].Kind;

    /// <summary>Início do trecho informado, em segundos.</summary>
    public double SegmentStartSeconds(int index) =>
        _segments[Math.Clamp(index, 0, _segments.Count - 1)].StartSeconds;

    /// <summary>Percentual de partida do trecho informado.</summary>
    public double SegmentStartPercent(int index) =>
        _segments[Math.Clamp(index, 0, _segments.Count - 1)].FromPercent;

    /// <summary>Percentual de chegada do trecho informado.</summary>
    public double SegmentEndPercent(int index) =>
        _segments[Math.Clamp(index, 0, _segments.Count - 1)].ToPercent;

    /// <summary>Duração do trecho informado.</summary>
    public double SegmentSeconds(int index)
    {
        Segment segment = _segments[Math.Clamp(index, 0, _segments.Count - 1)];
        return segment.EndSeconds - segment.StartSeconds;
    }

    /// <summary>Duração do primeiro trecho (usada para nomear a pegada).</summary>
    public double FirstSegmentSeconds => SegmentSeconds(0);

    private Segment SegmentAt(double elapsedSeconds)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            if (elapsedSeconds < _segments[i].EndSeconds)
            {
                return _segments[i];
            }
        }

        return _segments[^1];
    }

    private readonly struct Segment
    {
        public Segment(
            DrillSegmentKind kind,
            double startSeconds,
            double endSeconds,
            double fromPercent,
            double toPercent)
        {
            Kind = kind;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            FromPercent = fromPercent;
            ToPercent = toPercent;
        }

        public DrillSegmentKind Kind { get; }

        public double StartSeconds { get; }

        public double EndSeconds { get; }

        public double FromPercent { get; }

        public double ToPercent { get; }

        public double PercentAt(double elapsedSeconds)
        {
            double span = EndSeconds - StartSeconds;
            if (span <= 0)
            {
                return ToPercent;
            }

            double ratio = Math.Clamp((elapsedSeconds - StartSeconds) / span, 0, 1);
            return FromPercent + ((ToPercent - FromPercent) * ratio);
        }
    }
}
