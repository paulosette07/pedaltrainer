using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Executa um programa de treino: espera o pedal voltar ao repouso, dispara a repetição,
/// acompanha a curva-alvo, pontua e passa para a próxima etapa. Determinístico e testável —
/// o relógio entra por parâmetro.
/// </summary>
public sealed class DrillRunner
{
    /// <summary>Tempo mostrando a nota da repetição antes de seguir.</summary>
    public const double ResultHoldSeconds = 1.4;

    /// <summary>Quanto o pedal precisa subir acima do repouso para disparar a repetição.</summary>
    public const double TriggerDeltaPercent = 2.0;

    /// <summary>Passo dos alvos sorteados, em pontos percentuais.</summary>
    public const double RandomTargetStepPercent = 5.0;

    /// <summary>Máximo de resultados guardados em memória.</summary>
    public const int MaxResults = 240;

    private readonly Random _random;
    private readonly List<DrillRepetitionResult> _results = new();

    private DrillProgram? _program;
    private DrillProfile? _profile;
    private DrillRepetitionAnalyzer? _analyzer;
    private IReadOnlyList<DrillCurvePoint>? _targetCurve;
    private DrillRepetitionResult? _lastResult;
    private string _statusMessage = "Escolha um programa e clique em Iniciar.";
    private int _stepIndex;
    private int _repetitionIndex;
    private int _repetitionsDone;
    private double _targetLevelPercent = -1;
    private double _previousTargetPercent = -1;
    private double _repetitionStartSeconds;
    private double _repetitionDoneSeconds;
    private double _valuePercent;
    private bool _armedAtRest;

    public DrillRunner(Random? random = null)
    {
        _random = random ?? new Random();
    }

    /// <summary>Estágio atual da execução.</summary>
    public DrillStage Stage { get; private set; } = DrillStage.Idle;

    /// <summary>Programa em execução (nulo quando parado).</summary>
    public DrillProgram? Program => _program;

    /// <summary>Repetições concluídas, na ordem.</summary>
    public IReadOnlyList<DrillRepetitionResult> Results => _results;

    /// <summary>Resultado da última repetição concluída.</summary>
    public DrillRepetitionResult? LastResult => _lastResult;

    /// <summary>Média das notas já obtidas.</summary>
    public double AverageScore
    {
        get
        {
            if (_results.Count == 0)
            {
                return 0;
            }

            double sum = 0;
            foreach (DrillRepetitionResult result in _results)
            {
                sum += result.Score;
            }

            return sum / _results.Count;
        }
    }

    /// <summary>Melhor nota do programa.</summary>
    public double BestScore
    {
        get
        {
            double best = 0;
            foreach (DrillRepetitionResult result in _results)
            {
                best = Math.Max(best, result.Score);
            }

            return best;
        }
    }

    /// <summary>Último instantâneo publicado.</summary>
    public DrillUpdate Latest { get; private set; } = DrillUpdate.Empty;

    /// <summary>Etapa em execução no momento.</summary>
    public DrillStep? CurrentStep =>
        _program is not null && _stepIndex >= 0 && _stepIndex < _program.Steps.Count
            ? _program.Steps[_stepIndex]
            : null;

    /// <summary>Inicia um programa do começo.</summary>
    public bool Start(DrillProgram program, double nowSeconds)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (!program.TryValidate(out string error))
        {
            _program = null;
            Stage = DrillStage.Idle;
            _statusMessage = error;
            Latest = BuildUpdate(null, 0, nowSeconds);
            return false;
        }

        _program = program;
        _results.Clear();
        _lastResult = null;
        _stepIndex = 0;
        _repetitionIndex = 0;
        _repetitionsDone = 0;
        _previousTargetPercent = -1;
        _repetitionStartSeconds = nowSeconds;
        _repetitionDoneSeconds = nowSeconds;
        PrepareRepetition();

        Latest = BuildUpdate(CurrentStep, _valuePercent, nowSeconds);
        return true;
    }

    /// <summary>Interrompe o programa e volta ao repouso.</summary>
    public DrillUpdate Stop(double nowSeconds)
    {
        _program = null;
        _profile = null;
        _analyzer = null;
        _targetCurve = null;
        _stepIndex = 0;
        _repetitionIndex = 0;
        _repetitionsDone = 0;
        _targetLevelPercent = -1;
        _armedAtRest = false;
        Stage = DrillStage.Idle;
        _statusMessage = "Programa parado. Escolha um programa e clique em Iniciar.";

        Latest = BuildUpdate(null, _valuePercent, nowSeconds);
        return Latest;
    }

    /// <summary>Pula a repetição atual sem pontuar e segue para a próxima.</summary>
    public DrillUpdate Skip(double nowSeconds)
    {
        if (Stage is DrillStage.Idle or DrillStage.Complete)
        {
            return Latest;
        }

        Advance(nowSeconds);
        Latest = BuildUpdate(CurrentStep, _valuePercent, nowSeconds);
        return Latest;
    }

    /// <summary>Chama a cada quadro do motor com a leitura atual dos pedais.</summary>
    public DrillUpdate Update(in PedalReading pedals, double nowSeconds)
    {
        DrillStep? step = CurrentStep;
        DrillPedal pedal = step?.Pedal ?? _program?.PrimaryPedal ?? DrillPedal.Brake;
        double value = PedalValue(pedals, pedal);
        _valuePercent = value;

        switch (Stage)
        {
            case DrillStage.Armed:
                UpdateArmed(value, nowSeconds);
                break;
            case DrillStage.Running:
                UpdateRunning(value, nowSeconds);
                break;
            case DrillStage.RepetitionDone:
                if (nowSeconds - _repetitionDoneSeconds >= ResultHoldSeconds)
                {
                    Advance(nowSeconds);
                }

                break;
        }

        Latest = BuildUpdate(step, value, nowSeconds);
        return Latest;
    }

    private void UpdateArmed(double value, double nowSeconds)
    {
        DrillStep? step = CurrentStep;
        if (step is null || _profile is null || _program is null)
        {
            return;
        }

        double rest = _program.RestPercent;

        if (value <= rest)
        {
            _armedAtRest = true;
            _statusMessage = "Pedal no repouso — comece o movimento quando quiser.";
            return;
        }

        if (_armedAtRest && value >= rest + TriggerDeltaPercent)
        {
            BeginRepetition(nowSeconds);
        }
    }

    private void UpdateRunning(double value, double nowSeconds)
    {
        if (_profile is null || _analyzer is null || CurrentStep is null)
        {
            return;
        }

        double elapsed = nowSeconds - _repetitionStartSeconds;

        if (elapsed >= _profile.DurationSeconds)
        {
            FinishRepetition(nowSeconds);
            return;
        }

        if (elapsed >= 0)
        {
            _analyzer.Add(elapsed, value);
        }
    }

    private void BeginRepetition(double nowSeconds)
    {
        DrillStep? step = CurrentStep;
        if (step is null || _profile is null)
        {
            return;
        }

        _analyzer = new DrillRepetitionAnalyzer(_profile);
        _repetitionStartSeconds = nowSeconds;
        Stage = DrillStage.Running;
        _statusMessage = DrillInstructor.DescribeWithPedal(_profile, 0);
    }

    private void FinishRepetition(double nowSeconds)
    {
        DrillStep? step = CurrentStep;
        if (step is null || _analyzer is null)
        {
            return;
        }

        DrillRepetitionResult result = _analyzer.Complete(_repetitionIndex + 1, step.Repetitions);

        _lastResult = result;
        _results.Add(result);

        if (_results.Count > MaxResults)
        {
            _results.RemoveAt(0);
        }

        _repetitionsDone++;
        Stage = DrillStage.RepetitionDone;
        _repetitionDoneSeconds = nowSeconds;
        _statusMessage = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Repetição {result.RepetitionNumber}/{result.RepetitionCount} · {result.Score:0} pts · nota {result.Grade}");
    }

    private void Advance(double nowSeconds)
    {
        if (_program is null)
        {
            return;
        }

        _repetitionIndex++;

        if (_stepIndex < _program.Steps.Count && _repetitionIndex >= _program.Steps[_stepIndex].Repetitions)
        {
            _stepIndex++;
            _repetitionIndex = 0;
        }

        if (_stepIndex >= _program.Steps.Count)
        {
            Stage = DrillStage.Complete;
            _profile = null;
            _analyzer = null;
            _targetCurve = null;
            _armedAtRest = false;
            _targetLevelPercent = -1;
            _statusMessage = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Programa concluído · {_repetitionsDone} repetições · média {AverageScore:0} pts.");
            return;
        }

        PrepareRepetition();
        _repetitionDoneSeconds = nowSeconds;
    }

    private void PrepareRepetition()
    {
        DrillStep? step = CurrentStep;
        if (step is null)
        {
            return;
        }

        _targetLevelPercent = DrawTarget(step);
        _profile = new DrillProfile(step, _targetLevelPercent);
        _targetCurve = DrillTargetCurveBuilder.Build(_profile);
        _analyzer = null;
        _armedAtRest = false;
        Stage = DrillStage.Armed;

        _statusMessage = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{DrillInstructor.StepTitle(step)} · repetição {_repetitionIndex + 1} de {step.Repetitions}. Solte o pedal para começar.");
    }

    private double DrawTarget(DrillStep step)
    {
        if (!step.IsRandom)
        {
            _previousTargetPercent = step.TargetPercent;
            return step.TargetPercent;
        }

        double min = step.RandomTargetMinPercent!.Value;
        double max = step.RandomTargetMaxPercent!.Value;
        int slots = Math.Max(1, (int)Math.Floor(((max - min) / RandomTargetStepPercent) + 0.5) + 1);

        double candidate = min;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            int slot = _random.Next(slots);
            candidate = Math.Min(max, min + (slot * RandomTargetStepPercent));

            if (Math.Abs(candidate - _previousTargetPercent) > 0.01 || slots <= 1)
            {
                break;
            }
        }

        _previousTargetPercent = candidate;
        return candidate;
    }

    private DrillUpdate BuildUpdate(DrillStep? step, double value, double nowSeconds)
    {
        double target = -1;
        double elapsed = 0;
        double progress = 0;
        string instruction = _statusMessage;

        if (_profile is not null && step is not null)
        {
            if (Stage == DrillStage.Running)
            {
                elapsed = Math.Max(0, nowSeconds - _repetitionStartSeconds);
                target = _profile.TargetAt(elapsed);
                progress = Math.Clamp(elapsed / _profile.DurationSeconds, 0, 1);
                instruction = DrillInstructor.DescribeWithPedal(_profile, _profile.SegmentIndexAt(elapsed));
            }
            else if (Stage == DrillStage.Armed)
            {
                target = _profile.SegmentEndPercent(0);
                instruction = DrillInstructor.DescribeWithPedal(_profile, 0);
            }
            else if (Stage == DrillStage.RepetitionDone)
            {
                elapsed = _profile.DurationSeconds;
                progress = 1;
                target = _profile.TargetAt(_profile.DurationSeconds);
            }
        }

        int total = _program?.TotalRepetitions ?? 0;
        double programProgress = total == 0
            ? 0
            : Math.Clamp((_repetitionsDone + progress) / total, 0, 1);

        if (Stage == DrillStage.Complete)
        {
            programProgress = 1;
        }

        return new DrillUpdate
        {
            Stage = Stage,
            ProgramName = _program?.Name ?? "Nenhum programa selecionado",
            StepLabel = step is null ? string.Empty : DrillInstructor.StepTitle(step),
            Instruction = instruction,
            Pedal = step?.Pedal ?? DrillPedal.Brake,
            TargetPercent = target,
            ValuePercent = value,
            TargetLevelPercent = _targetLevelPercent,
            TolerancePercent = step?.TolerancePercent ?? 5,
            RepetitionNumber = step is null ? 0 : _repetitionIndex + 1,
            RepetitionCount = step?.Repetitions ?? 0,
            StepNumber = _program is null ? 0 : _stepIndex + 1,
            StepCount = _program?.Steps.Count ?? 0,
            RepetitionsDone = _repetitionsDone,
            RepetitionsTotal = total,
            Progress = progress,
            ProgramProgress = programProgress,
            ElapsedSeconds = elapsed,
            RepetitionStartSeconds = _repetitionStartSeconds,
            LiveScore = _analyzer?.LiveScore ?? 0,
            AverageScore = AverageScore,
            BestScore = BestScore,
            LastResult = _lastResult,
            TargetCurve = _targetCurve,
            StatusMessage = _statusMessage
        };
    }

    private static double PedalValue(in PedalReading pedals, DrillPedal pedal) =>
        pedal == DrillPedal.Throttle ? pedals.ThrottlePercent : pedals.BrakePercent;
}
