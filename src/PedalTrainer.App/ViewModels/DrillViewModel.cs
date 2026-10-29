using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Tela de programas de treino: lista os arquivos de treino, executa o programa escolhido
/// e mostra a instrução do momento, a curva-alvo e as notas repetição a repetição.
/// </summary>
public sealed class DrillViewModel : ObservableObject
{
    private const int MaxPlotSamples = 8192;
    private const double WaitingWindowSeconds = 4.0;

    private readonly TrainingEngine _engine;
    private readonly DrillProgramStore _store;
    private readonly TraceSample[] _traceBuffer = new TraceSample[MaxPlotSamples];
    private readonly DrillCurvePoint[] _plotBuffer = new DrillCurvePoint[MaxPlotSamples];

    private DrillProgramEntry? _selectedProgram;
    private DrillRepetitionResult? _lastResult;
    private IReadOnlyList<DrillCurvePoint>? _plotCurve;
    private ReadOnlyMemory<DrillCurvePoint> _plotTrace = ReadOnlyMemory<DrillCurvePoint>.Empty;
    private double _cursorSeconds;
    private string _message = string.Empty;
    private string _programTitle = "Nenhum programa selecionado";
    private string _programDescription = string.Empty;
    private string _programSummary = string.Empty;
    private string _stageText = "PARADO";
    private string _instructionText = "Escolha um programa e clique em Iniciar.";
    private string _stepLabel = "—";
    private string _repetitionText = "—";
    private string _stepCounterText = "—";
    private string _targetText = "—";
    private string _scoreText = "--";
    private string _gradeText = string.Empty;
    private string _averageText = "-";
    private string _bestText = "-";
    private string _elapsedText = "0,0 s";
    private string _resultSummary = "Nenhuma repetição concluída.";
    private bool _isRunning;
    private bool _isStarted;
    private double _scoreValue;
    private double _progress;
    private double _programProgress;
    private double _brakePercent;
    private double _throttlePercent;
    private double _targetMarkerPercent = -1;
    private DrillPedal _activePedal = DrillPedal.Brake;

    public DrillViewModel(TrainingEngine engine, DrillProgramStore store)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));

        Programs = new ObservableCollection<DrillProgramEntry>();
        Results = new ObservableCollection<DrillResultItem>();
        StepPreview = new ObservableCollection<string>();

        StartCommand = new RelayCommand(StartDrill, () => !IsRunning && SelectedProgram?.Program is not null);
        StopCommand = new RelayCommand(StopDrill, () => IsRunning);
        SkipCommand = new RelayCommand(SkipRepetition, () => IsRunning);
        ReloadCommand = new RelayCommand(() => LoadLibrary(null));
        ImportCommand = new RelayCommand(ImportProgram);
        ExportCommand = new RelayCommand(ExportProgram, () => SelectedProgram?.Program is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);

        LoadLibrary(null);
    }

    /// <summary>Disparado quando o gráfico ou as barras precisam ser redesenhados.</summary>
    public event EventHandler? PlotDataChanged;

    public ObservableCollection<DrillProgramEntry> Programs { get; }

    public ObservableCollection<DrillResultItem> Results { get; }

    public ObservableCollection<string> StepPreview { get; }

    public IRelayCommand StartCommand { get; }

    public IRelayCommand StopCommand { get; }

    public IRelayCommand SkipCommand { get; }

    public IRelayCommand ReloadCommand { get; }

    public IRelayCommand ImportCommand { get; }

    public IRelayCommand ExportCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IRelayCommand RestoreDefaultsCommand { get; }

    /// <summary>Programa escolhido na lista.</summary>
    public DrillProgramEntry? SelectedProgram
    {
        get => _selectedProgram;
        set
        {
            if (!SetProperty(ref _selectedProgram, value))
            {
                return;
            }

            UpdateSelectedProgram();
        }
    }

    public string ProgramTitle { get => _programTitle; private set => SetProperty(ref _programTitle, value); }

    public string ProgramDescription { get => _programDescription; private set => SetProperty(ref _programDescription, value); }

    public string ProgramSummary { get => _programSummary; private set => SetProperty(ref _programSummary, value); }

    public string StageText { get => _stageText; private set => SetProperty(ref _stageText, value); }

    public string InstructionText { get => _instructionText; private set => SetProperty(ref _instructionText, value); }

    public string StepLabel { get => _stepLabel; private set => SetProperty(ref _stepLabel, value); }

    public string RepetitionText { get => _repetitionText; private set => SetProperty(ref _repetitionText, value); }

    public string StepCounterText { get => _stepCounterText; private set => SetProperty(ref _stepCounterText, value); }

    public string TargetText { get => _targetText; private set => SetProperty(ref _targetText, value); }

    public string ScoreText { get => _scoreText; private set => SetProperty(ref _scoreText, value); }

    public string GradeText { get => _gradeText; private set => SetProperty(ref _gradeText, value); }

    public string AverageText { get => _averageText; private set => SetProperty(ref _averageText, value); }

    public string BestText { get => _bestText; private set => SetProperty(ref _bestText, value); }

    public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

    public string ResultSummary { get => _resultSummary; private set => SetProperty(ref _resultSummary, value); }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Verdadeiro depois que algum programa foi iniciado (mantém o cartão visível).</summary>
    public bool IsStarted { get => _isStarted; private set => SetProperty(ref _isStarted, value); }

    public double ScoreValue { get => _scoreValue; private set => SetProperty(ref _scoreValue, value); }

    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }

    public double ProgramProgress { get => _programProgress; private set => SetProperty(ref _programProgress, value); }

    public double BrakePercent { get => _brakePercent; private set => SetProperty(ref _brakePercent, value); }

    public double ThrottlePercent { get => _throttlePercent; private set => SetProperty(ref _throttlePercent, value); }

    /// <summary>Marcador do pedal em uso; negativo esconde o marcador.</summary>
    public double TargetMarkerPercent { get => _targetMarkerPercent; private set => SetProperty(ref _targetMarkerPercent, value); }

    public DrillPedal ActivePedal { get => _activePedal; private set => SetProperty(ref _activePedal, value); }

    /// <summary>Curva-alvo desenhada no gráfico (nula antes da repetição começar).</summary>
    public IReadOnlyList<DrillCurvePoint>? PlotCurve => _plotCurve;

    /// <summary>Entrada do piloto, com o tempo relativo ao início da repetição.</summary>
    public ReadOnlyMemory<DrillCurvePoint> PlotTrace => _plotTrace;

    public double CursorSeconds => _cursorSeconds;

    /// <summary>Tolerância da etapa atual, desenhada como banda no gráfico.</summary>
    public double TolerancePercent { get; private set; } = 5;

    /// <summary>
    /// Aplica o instantâneo publicado pelo motor. O gráfico só é reconstruído quando a
    /// tela está visível, mas o estado e a lista de resultados são sempre atualizados.
    /// </summary>
    public void Apply(DrillUpdate update, bool renderPlot)
    {
        ArgumentNullException.ThrowIfNull(update);

        TrainingFrame frame = _engine.Latest;
        BrakePercent = frame.Pedals.BrakePercent;
        ThrottlePercent = frame.Pedals.ThrottlePercent;

        IsRunning = update.IsRunning;
        IsStarted = update.Stage != DrillStage.Idle || IsStarted;

        StageText = DrillStageNames.Label(update.Stage);
        InstructionText = update.Instruction;
        StepLabel = string.IsNullOrWhiteSpace(update.StepLabel) ? "—" : update.StepLabel;
        ProgramTitle = update.ProgramName;
        ActivePedal = update.Pedal;

        RepetitionText = update.RepetitionCount == 0
            ? "—"
            : string.Create(
                CultureInfo.CurrentCulture,
                $"Repetição {update.RepetitionNumber} de {update.RepetitionCount}");

        StepCounterText = update.StepCount == 0
            ? "—"
            : string.Create(CultureInfo.CurrentCulture, $"Etapa {update.StepNumber} de {update.StepCount}");

        TargetText = update.TargetLevelPercent < 0
            ? "—"
            : string.Create(CultureInfo.CurrentCulture, $"{update.TargetLevelPercent:0}%");

        ScoreValue = update.LiveScore;
        ScoreText = update.LiveScore > 0
            ? update.LiveScore.ToString("0", CultureInfo.CurrentCulture)
            : "--";
        GradeText = update.LiveScore > 0 ? ScoreGrade.From(update.LiveScore) : string.Empty;

        AverageText = update.RepetitionsDone > 0
            ? update.AverageScore.ToString("0", CultureInfo.CurrentCulture)
            : "-";
        BestText = update.RepetitionsDone > 0
            ? update.BestScore.ToString("0", CultureInfo.CurrentCulture)
            : "-";

        ElapsedText = string.Create(CultureInfo.CurrentCulture, $"{update.ElapsedSeconds:0.0} s");
        Progress = Math.Clamp(update.Progress, 0, 1);
        ProgramProgress = Math.Clamp(update.ProgramProgress, 0, 1);
        TolerancePercent = update.TolerancePercent;

        TargetMarkerPercent = update.TargetPercent >= 0 ? update.TargetPercent : -1;

        RegisterResult(update);

        if (!renderPlot)
        {
            return;
        }

        UpdatePlot(update);
        PlotDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Recarrega a lista de programas do disco.</summary>
    public void LoadLibrary(string? selectPath)
    {
        string? previous = selectPath ?? _selectedProgram?.FilePath;
        IReadOnlyList<DrillProgramEntry> entries = _store.LoadLibrary();

        Programs.Clear();
        foreach (DrillProgramEntry entry in entries)
        {
            Programs.Add(entry);
        }

        SelectedProgram =
            Programs.FirstOrDefault(entry => previous is not null && DrillProgramStore.SamePath(entry.FilePath, previous))
            ?? Programs.FirstOrDefault(entry => entry.IsValid)
            ?? Programs.FirstOrDefault();

        if (Programs.Count == 0)
        {
            Message = "Nenhum programa encontrado na pasta de treinos.";
        }
    }

    private void UpdateSelectedProgram()
    {
        StepPreview.Clear();

        if (_selectedProgram?.Program is not { } program)
        {
            ProgramTitle = "Nenhum programa selecionado";
            ProgramDescription = _selectedProgram?.Error ?? "Escolha um arquivo de treino na lista.";
            ProgramSummary = string.Empty;
            RefreshCommands();
            return;
        }

        ProgramTitle = program.Name;
        ProgramDescription = string.IsNullOrWhiteSpace(program.Description)
            ? "Sem descrição."
            : program.Description;
        ProgramSummary = _selectedProgram.Summary;

        for (var index = 0; index < program.Steps.Count; index++)
        {
            DrillStep step = program.Steps[index];

            StepPreview.Add(string.Create(
                CultureInfo.CurrentCulture,
                $"{index + 1}. {DrillInstructor.StepTitle(step)} · {step.Repetitions}× · {DrillInstructor.PressPace(step)}"));
        }

        if (!IsRunning)
        {
            StepLabel = "—";
            InstructionText = "Clique em Iniciar e mantenha o pedal solto para a primeira repetição.";
        }

        RefreshCommands();
    }

    private void StartDrill()
    {
        if (_selectedProgram?.Program is not { } program)
        {
            return;
        }

        Results.Clear();
        _lastResult = null;
        ResultSummary = "Nenhuma repetição concluída.";

        _engine.StartDrill(program.Clone());
        IsStarted = true;
        Message = string.Create(
            CultureInfo.CurrentCulture,
            $"Executando “{program.Name}”. Solte o pedal para começar a primeira repetição.");
    }

    private void StopDrill()
    {
        _engine.StopDrill();
        Message = "Programa interrompido.";
        IsStarted = false;
    }

    private void SkipRepetition()
    {
        _engine.SkipDrillRepetition();
        Message = "Repetição pulada.";
    }

    private void ImportProgram()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar arquivo de treino",
            Filter = DrillProgramFile.BuildFileDialogFilter(),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!DrillProgramFile.TryLoad(dialog.FileName, out DrillProgram? program, out string error) || program is null)
        {
            Message = error;
            return;
        }

        string destination = _store.SuggestPath(program);

        if (!DrillProgramFile.Save(program, destination, out string saveError))
        {
            Message = saveError;
            return;
        }

        LoadLibrary(destination);
        Message = string.Create(
            CultureInfo.CurrentCulture,
            $"“{program.Name}” importado para {Path.GetFileName(destination)}.");
    }

    private void ExportProgram()
    {
        if (_selectedProgram?.Program is not { } program)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar programa de treino",
            Filter = DrillProgramFile.BuildFileDialogFilter(),
            FileName = Path.GetFileName(_store.SuggestPath(program)),
            AddExtension = true,
            DefaultExt = DrillProgramFile.Extension
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Message = DrillProgramFile.Save(program, dialog.FileName, out string error)
            ? $"Programa exportado para {Path.GetFileName(dialog.FileName)}."
            : error;
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_store.Directory}\"",
                UseShellExecute = true
            });

            Message = _store.Directory;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Message = $"Não foi possível abrir a pasta: {ex.Message}";
        }
    }

    private void RestoreDefaults()
    {
        if (!_store.EnsureDefaultFiles(overwrite: true, out string error))
        {
            Message = error;
            return;
        }

        LoadLibrary(null);
        Message = "Programas padrão regravados na pasta de treinos.";
    }

    private void RegisterResult(DrillUpdate update)
    {
        if (update.LastResult is not { } result || ReferenceEquals(result, _lastResult))
        {
            return;
        }

        _lastResult = result;
        Results.Insert(0, DrillResultItem.From(result));

        while (Results.Count > 40)
        {
            Results.RemoveAt(Results.Count - 1);
        }

        ResultSummary = string.Create(
            CultureInfo.CurrentCulture,
            $"{update.RepetitionsDone} de {update.RepetitionsTotal} repetições · média {update.AverageScore:0} pts");
    }

    private void UpdatePlot(DrillUpdate update)
    {
        bool running = update.Stage is DrillStage.Running or DrillStage.RepetitionDone;
        _plotCurve = running ? update.TargetCurve : null;

        double anchor = running ? update.RepetitionStartSeconds : _engine.NowSeconds - WaitingWindowSeconds;

        int copied = _engine.CopyTrace(_traceBuffer, anchor);
        int count = 0;

        for (var index = 0; index < copied && count < _plotBuffer.Length; index++)
        {
            TraceSample sample = _traceBuffer[index];
            double value = update.Pedal == DrillPedal.Throttle
                ? sample.ThrottlePercent
                : sample.BrakePercent;

            _plotBuffer[count++] = new DrillCurvePoint(sample.TimeSeconds - anchor, value);
        }

        _plotTrace = new ReadOnlyMemory<DrillCurvePoint>(_plotBuffer, 0, count);
        _cursorSeconds = running ? update.ElapsedSeconds : 0;
    }

    private void RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }
}
