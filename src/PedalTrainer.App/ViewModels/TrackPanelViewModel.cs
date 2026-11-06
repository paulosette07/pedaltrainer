using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Cartão "pista" da aba de treino: escolhe o circuito, mostra o mapa e deixa o piloto
/// decidir a referência — a volta ideal do sistema ou uma telemetria dele mesmo.
/// </summary>
public sealed class TrackPanelViewModel : ObservableObject
{
    private readonly IShellActions _shell;

    private TrackDefinition _selectedTrack;
    private TrackReferenceSource? _selectedReference;
    private string _message = string.Empty;
    private bool _ignoreReferenceChange;

    public TrackPanelViewModel(IShellActions shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));

        _selectedTrack = shell.SelectedTrack;

        Tracks = new ObservableCollection<TrackDefinition>(shell.Tracks);

        LoadReferenceFileCommand = new RelayCommand(LoadReferenceFile);
        ImportTrackMapCommand = new RelayCommand(ImportTrackMap);
        UseSystemReferenceCommand = new RelayCommand(UseSystemReference);
        ReloadReferencesCommand = new RelayCommand(ReloadReferences);

        ReloadReferences();
    }

    /// <summary>Pistas do sistema e importadas.</summary>
    public ObservableCollection<TrackDefinition> Tracks { get; }

    /// <summary>Referências disponíveis para a pista atual.</summary>
    public ObservableCollection<TrackReferenceSource> References { get; } = new();

    public IRelayCommand LoadReferenceFileCommand { get; }

    public IRelayCommand ImportTrackMapCommand { get; }

    public IRelayCommand UseSystemReferenceCommand { get; }

    public IRelayCommand ReloadReferencesCommand { get; }

    /// <summary>Pista escolhida. A troca já carrega a referência dela.</summary>
    public TrackDefinition SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedTrack))
            {
                return;
            }

            if (!SetProperty(ref _selectedTrack, value))
            {
                return;
            }

            _shell.SelectTrack(value);
            ReloadReferences();
            Message = $"{value.Name} selecionada.";
        }
    }

    /// <summary>Referência em uso na pista.</summary>
    public TrackReferenceSource? SelectedReference
    {
        get => _selectedReference;
        set
        {
            if (value is null || _ignoreReferenceChange)
            {
                return;
            }

            if (!SetProperty(ref _selectedReference, value))
            {
                return;
            }

            if (!_shell.LoadReferenceSource(value, out string? error))
            {
                Message = error ?? "Não foi possível carregar a referência.";
                return;
            }

            OnPropertyChanged(nameof(ReferenceSummary));
            Message = value.IsSystem
                ? "Volta ideal do sistema carregada."
                : $"Referência {value.Label} carregada.";
        }
    }

    /// <summary>Nome da pista em destaque.</summary>
    public string TrackTitle => _selectedTrack.Name;

    /// <summary>Local, comprimento e curvas da pista.</summary>
    public string TrackDetail => string.Create(
        CultureInfo.CurrentCulture,
        $"{_selectedTrack.CityAndCountry} · {_selectedTrack.LengthMeters:0} m · {_selectedTrack.Corners.Count} curvas");

    /// <summary>Resumo da referência carregada (duração e zonas de frenagem).</summary>
    public string ReferenceSummary
    {
        get
        {
            if (_shell.Engine.Session.ReferenceLap is not { } lap)
            {
                return "Nenhuma referência carregada.";
            }

            return string.Create(
                CultureInfo.CurrentCulture,
                $"{lap.DurationSeconds:0.0} s · {_shell.Engine.Session.Zones.Count} zonas de frenagem");
        }
    }

    /// <summary>Dica/erro exibido abaixo da lista.</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>Recarrega a lista de referências da pista atual.</summary>
    public void ReloadReferences()
    {
        _ignoreReferenceChange = true;

        try
        {
            References.Clear();

            foreach (TrackReferenceSource source in _shell.ListReferenceSources())
            {
                References.Add(source);
            }

            _selectedReference = References.Count > 0 ? References[0] : null;
        }
        finally
        {
            _ignoreReferenceChange = false;
        }

        OnPropertyChanged(nameof(SelectedReference));
        OnPropertyChanged(nameof(TrackTitle));
        OnPropertyChanged(nameof(TrackDetail));
        OnPropertyChanged(nameof(ReferenceSummary));
    }

    /// <summary>Atualiza o resumo depois que o shell trocou a referência por conta própria.</summary>
    public void NotifyReferenceChanged()
    {
        OnPropertyChanged(nameof(ReferenceSummary));
        OnPropertyChanged(nameof(TrackDetail));
    }

    /// <summary>Marca como selecionado o arquivo que o shell acabou de carregar.</summary>
    public void SelectReferenceFromPath(string path)
    {
        TrackReferenceSource? match = FindSource(path);

        if (match is null)
        {
            return;
        }

        _ignoreReferenceChange = true;

        try
        {
            _selectedReference = match;
        }
        finally
        {
            _ignoreReferenceChange = false;
        }

        OnPropertyChanged(nameof(SelectedReference));
        NotifyReferenceChanged();
    }

    /// <summary>Carrega uma telemetria do disco para esta pista.</summary>
    private void LoadReferenceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Carregar telemetria de referência",
            Filter = _shell.ReferenceFileFilter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string imported;

        try
        {
            imported = _shell.ImportReferenceFile(dialog.FileName);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            Message = exception.Message;
            return;
        }

        ReloadReferences();

        TrackReferenceSource? source = FindSource(imported);
        if (source is not null)
        {
            SelectedReference = source;
        }
    }

    /// <summary>Importa um traçado (JSON) e passa a usá-lo.</summary>
    private void ImportTrackMap()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar traçado da pista",
            Filter = _shell.TrackMapFileFilter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        TrackDefinition? imported = _shell.ImportTrackMap(dialog.FileName, out string? error);

        if (imported is null)
        {
            Message = error ?? "Não foi possível importar o traçado.";
            return;
        }

        if (!Tracks.Contains(imported))
        {
            Tracks.Insert(0, imported);
        }

        if (ReferenceEquals(imported, _selectedTrack))
        {
            ReloadReferences();
        }
        else
        {
            SelectedTrack = imported;
        }

        Message = $"Traçado de {imported.Name} importado.";
    }

    /// <summary>Volta para a volta ideal do sistema.</summary>
    private void UseSystemReference()
    {
        TrackReferenceSource? system = References.Count > 0 ? References[0] : null;

        if (system is null)
        {
            ReloadReferences();
            system = References.Count > 0 ? References[0] : null;
        }

        if (system is not null)
        {
            SelectedReference = system;
        }
    }

    private TrackReferenceSource? FindSource(string path)
    {
        foreach (TrackReferenceSource source in References)
        {
            if (!source.IsSystem && string.Equals(source.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }
        }

        return null;
    }
}
