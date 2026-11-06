using System;

namespace PedalTrainer.Core.Tracks;

/// <summary>Erro ao ler ou gravar o traçado de uma pista (arquivo inválido, incompleto...).</summary>
public sealed class TrackMapException : Exception
{
    public TrackMapException(string message)
        : base(message)
    {
    }

    public TrackMapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
