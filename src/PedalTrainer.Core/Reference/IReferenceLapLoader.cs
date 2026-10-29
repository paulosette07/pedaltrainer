using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>Contrato dos carregadores de volta de referência.</summary>
public interface IReferenceLapLoader
{
    /// <summary>Nome legível do formato (usado em mensagens e no filtro de arquivos).</summary>
    string FormatName { get; }

    /// <summary>Extensões suportadas, sem o ponto, em minúsculas.</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>Indica se este carregador entende o arquivo informado.</summary>
    bool CanLoad(string path);

    /// <summary>Lê o arquivo e devolve a volta de referência já validada.</summary>
    ReferenceLap Load(string path);
}

/// <summary>Erro de leitura ou validação de um arquivo de referência.</summary>
public sealed class ReferenceLapException : Exception
{
    public ReferenceLapException(string message)
        : base(message)
    {
    }

    public ReferenceLapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
