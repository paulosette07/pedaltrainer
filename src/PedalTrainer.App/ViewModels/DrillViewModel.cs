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

    private readonly TrainingEngine _engine;
    private readonly DrillProgramStore _store;
    private readonly TraceSample[] _traceBuffer = new TraceSample[MaxPlotSamples];
    private readonly DrillCurvePoint[] _plotBuffer = new DrillCurvePoint[MaxPlotSamples];

    private DrillProgramEntry? _selectedProgram;
    private DrillRepetitionResult? _lastResult;
    private IReadOnlyList<DrillCurvePoint>? _plotCurve;
    private ReadOnlyMemory<DrillCurvePoint> _plotTrace = ReadOnlyMemory<DrillCurvePoint>.Empty;
    private IReadOnlyList<DrillCurvePoint>? _previewCurve;
    private IReadOnlyList<DrillCurveStep> _previewPlan = Array.Empty<DrillCurveStep>();
    private IReadOnlyList<DrillCurveStep> _shownPlan = Array.Empty<DrillCurveStep>();
    private string _previewSummary = string.Empty;
    private string _previewCaption = string.Empty;
    private string _curveSummary = string.Empty;
    private string _curvePlanTitle = "CURVA-ALVO";
    private string _nextStepText = string.Empty;
    private double _previewTolerance = 5;
    private int _shownPlanIndex = -1;
    private bool _isCurvePreview;
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
        CurveSteps = new ObservableCollection<DrillCurveStepItem>();

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

    /// <summary>Trechos da curva mostrada (subir, segurar, soltar), na ordem.</summary>
    public ObservableCollection<DrillCurveStepItem> CurveSteps { get; }

    /// <summary>Título da tira de trechos ("PRÓXIMA CURVA", "CURVA EM ANDAMENTO").</summary>
    public string CurvePlanTitle { get => _curvePlanTitle; private set => SetProperty(ref _curvePlanTitle, value); }

    /// <summary>Resumo da curva em texto ("pico 90% · 3,3 s").</summary>
    public string CurveSummary { get => _curveSummary; private set => SetProperty(ref _curveSummary, value); }

    /// <summary>Aviso de que a curva muda na etapa seguinte (vazio quando não muda).</summary>
    public string NextStepText { get => _nextStepText; private set => SetProperty(ref _nextStepText, value); }

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

    /// <summary>
    /// Curva desenhada no gráfico: a da repetição em execução, a da próxima repetição
    /// (estágio de espera) ou a prévia do programa escolhido.
    /// </summary>
    public IReadOnlyList<DrillCurvePoint>? PlotCurve => _plotCurve;

    /// <summary>Verdadeiro quando o gráfico mostra a prévia, sem entrada do piloto.</summary>
    public bool IsCurvePreview => _isCurvePreview;

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
            BuildPreviewCurve(null);
            RefreshCommands();
            RefreshIdlePreview();
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
                $"{index + 1}. {DrillInstructor.StepTitle(step)} · {step.Repetitions}× · " +
                $"pisa {DrillInstructor.PressPace(step)}, solta {DrillInstructor.ReleasePace(step)}"));
        }

        BuildPreviewCurve(program);

        if (!IsRunning)
        {
            StepLabel = "—";
            InstructionText = "Clique em Iniciar e mantenha o pedal solto para a primeira repetição.";
        }

        RefreshCommands();
        RefreshIdlePreview();
    }

    /// <summary>
    /// Com o treino parado, troca o gráfico pela prévia do programa escolhido na hora: o
    /// piloto vê o formato da curva enquanto decide o que treinar.
    /// </summary>
    private void RefreshIdlePreview()
    {
        if (IsRunning)
        {
            return;
        }

        ShowLibraryPreview();
        PlotDataChanged?.Invoke(this, EventArgs.Empty);
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
        if (update.Stage is DrillStage.Armed or DrillStage.Running or DrillStage.RepetitionDone)
        {
            ShowRepetitionCurve(update);
        }
        else
        {
            ShowLibraryPreview();
        }
    }

    /// <summary>
    /// Curva da repetição do momento: no estágio de espera ela aparece como prévia
    /// tracejada, para o piloto saber o formato antes de pisar no pedal.
    /// </summary>
    private void ShowRepetitionCurve(DrillUpdate update)
    {
        bool preview = update.Stage == DrillStage.Armed;

        SetShownCurve(
            update.TargetCurve,
            update.CurvePlan,
            update.CurveSummary,
            preview
                ? "PRÓXIMA CURVA"
                : update.Stage == DrillStage.Running ? "CURVA EM ANDAMENTO" : "CURVA DA REPETIÇÃO",
            update.TolerancePercent,
            update.CurvePlanIndex,
            preview);

        NextStepText = preview && !string.IsNullOrWhiteSpace(update.NextStepLabel)
            ? $"A etapa seguinte muda a curva: {update.NextStepLabel}."
            : string.Empty;

        if (preview)
        {
            _plotTrace = ReadOnlyMemory<DrillCurvePoint>.Empty;
            _cursorSeconds = 0;
            return;
        }

        CopyLiveTrace(update);
        _cursorSeconds = update.ElapsedSeconds;
    }

    /// <summary>
    /// Sem programa em execução, o gráfico mostra a curva do primeiro exercício do arquivo
    /// escolhido: dá para ver o formato antes de apertar Iniciar.
    /// </summary>
    private void ShowLibraryPreview()
    {
        SetShownCurve(
            _previewCurve,
            _previewPlan,
            _previewSummary,
            _previewCaption,
            _previewTolerance,
            -1,
            _previewCurve is not null);

        _plotTrace = ReadOnlyMemory<DrillCurvePoint>.Empty;
        _cursorSeconds = 0;
        NextStepText = string.Empty;
    }

    private void SetShownCurve(
        IReadOnlyList<DrillCurvePoint>? curve,
        IReadOnlyList<DrillCurveStep> plan,
        string summary,
        string title,
        double tolerance,
        int planIndex,
        bool isPreview)
    {
        _plotCurve = curve;
        _isCurvePreview = isPreview;
        TolerancePercent = tolerance;
        CurvePlanTitle = title;
        CurveSummary = summary;

        UpdateCurveSteps(plan, planIndex);
    }

    /// <summary>Recria a tira de trechos só quando a curva ou o trecho atual mudam.</summary>
    private void UpdateCurveSteps(IReadOnlyList<DrillCurveStep> plan, int planIndex)
    {
        if (ReferenceEquals(plan, _shownPlan) && planIndex == _shownPlanIndex)
        {
            return;
        }

        _shownPlan = plan;
        _shownPlanIndex = planIndex;

        CurveSteps.Clear();

        foreach (DrillCurveStepItem item in DrillCurveStepItem.From(plan, planIndex))
        {
            CurveSteps.Add(item);
        }
    }

    /// <summary>Copia a entrada do piloto, com o tempo relativo ao início da repetição.</summary>
    private void CopyLiveTrace(DrillUpdate update)
    {
        double anchor = update.RepetitionStartSeconds;
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
    }

    /// <summary>
    /// Monta o desenho da primeira etapa do programa escolhido. Quando a etapa sorteia o
    /// alvo, o desenho usa o meio da faixa — a forma da curva é a mesma.
    /// </summary>
    private void BuildPreviewCurve(DrillProgram? program)
    {
        _previewCurve = null;
        _previewPlan = Array.Empty<DrillCurveStep>();
        _previewSummary = string.Empty;
        _previewCaption = "CURVA-ALVO";
        _previewTolerance = 5;

        DrillStep? first = program?.Steps.FirstOrDefault(step => step.Segments.Count > 0);
        if (first is null || DrillCurvePreview.TryCreate(first) is not { } preview)
        {
            return;
        }

        _previewCurve = preview.Curve;
        _previewPlan = preview.Plan;
        _previewSummary = preview.Summary;
        _previewTolerance = first.TolerancePercent;
        _previewCaption = string.Create(
            CultureInfo.CurrentCulture,
            $"PRÉVIA · {DrillInstructor.StepTitle(first)}");
    }

    private void RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }
}
