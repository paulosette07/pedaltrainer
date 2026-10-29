using System;
using System.Collections.Generic;
using System.Text;

namespace PedalTrainer.Core.Input.Pedals;

/// <summary>
/// Catálogo dos conjuntos de pedais vendidos hoje. Cada entrada guarda o que o app
/// precisa saber para tratar o sinal daquele hardware: tecnologia do freio, número de
/// pedais e os ajustes de curva, zona morta e filtro que funcionam melhor nele.
/// A ordem é significativa: o primeiro modelo que casa com o nome do controlador vence,
/// então os mais específicos vêm antes dos genéricos.
/// </summary>
public static class PedalModelCatalog
{
    /// <summary>Identificador do item "não listado" (mantém a calibração como está).</summary>
    public const string CatchAllId = "";

    private const int ThreePedals = 3;
    private const int TwoPedals = 2;

    // Ajustes base por tecnologia de freio.
    private const double LoadCellExponent = 1.0;
    private const double LoadCellBrakeDeadzone = 1.5;
    private const double LoadCellThrottleDeadzone = 1.0;
    private const double LoadCellSmoothing = 0.55;

    private const double HallExponent = 1.20;
    private const double HallBrakeDeadzone = 2.0;
    private const double HallThrottleDeadzone = 1.5;
    private const double HallSmoothing = 0.45;

    private const double PotExponent = 1.45;
    private const double PotBrakeDeadzone = 3.5;
    private const double PotThrottleDeadzone = 2.0;
    private const double PotSmoothing = 0.35;

    private static readonly PedalModel CatchAll = new(
        CatchAllId,
        string.Empty,
        "Outro conjunto / não listado",
        PedalSensorType.Potentiometer,
        ThreePedals,
        LoadCellExponent,
        LoadCellExponent,
        LoadCellBrakeDeadzone,
        LoadCellThrottleDeadzone,
        LoadCellSmoothing,
        Array.Empty<string>(),
        "Sem perfil de fábrica: a calibração manual continua valendo.");

    /// <summary>Todos os modelos, do mais específico ao mais genérico, sem o item "não listado".</summary>
    public static IReadOnlyList<PedalModel> All { get; } = BuildAll();

    /// <summary>Item usado quando o usuário não sabe (ou não quer) escolher um modelo.</summary>
    public static PedalModel Unknown => CatchAll;

    /// <summary>Marcas presentes no catálogo, em ordem alfabética.</summary>
    public static IReadOnlyList<string> Brands { get; } = BuildBrands();

    /// <summary>Busca um modelo pelo identificador; devolve o "não listado" quando não achar.</summary>
    public static PedalModel Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return CatchAll;
        }

        foreach (PedalModel model in All)
        {
            if (string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }
        }

        return CatchAll;
    }

    /// <summary>Modelos de uma marca, na ordem do catálogo.</summary>
    public static IReadOnlyList<PedalModel> ForBrand(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return Array.Empty<PedalModel>();
        }

        var found = new List<PedalModel>();
        foreach (PedalModel model in All)
        {
            if (string.Equals(model.Brand, brand, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(model);
            }
        }

        return found;
    }

    /// <summary>
    /// Tenta reconhecer o modelo pelo nome que o DirectInput reportou
    /// (por exemplo "Fanatec ClubSport Pedals V3"). Devolve nulo quando nada casa.
    /// </summary>
    public static PedalModel? Match(string? deviceName, string? productName = null)
    {
        if (string.IsNullOrWhiteSpace(deviceName) && string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        string haystack = Normalize($"{deviceName} {productName}");

        foreach (PedalModel model in All)
        {
            foreach (string keyword in model.MatchKeywords)
            {
                if (haystack.Contains(Normalize(keyword), StringComparison.Ordinal))
                {
                    return model;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Deixa o texto comparável: minúsculo e com hífen/underscore trocados por espaço,
    /// para "T-LCM" e "t lcm" caírem no mesmo balde.
    /// </summary>
    private static string Normalize(string text)
    {
        string lower = text.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);

        foreach (char character in lower)
        {
            builder.Append(character is '-' or '_' ? ' ' : character);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<PedalModel> BuildAll()
    {
        var models = new List<PedalModel>(32);

        AddFanatec(models);
        AddMoza(models);
        AddSimagic(models);
        AddHeusinkveld(models);
        AddAsetek(models);
        AddLogitech(models);
        AddThrustmaster(models);
        AddCustom(models);

        return models;
    }

    private static void AddFanatec(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "fanatec-clubsport-v3", "Fanatec", "ClubSport Pedals V3",
            PedalSensorType.LoadCell, ThreePedals,
            1.10, 1.00, 1.5, 1.0, 0.55,
            new[] { "clubsport pedals v3", "clubsport v3", "csp v3" },
            "Load Cell de 90 kg e curso curto. A curva 1,10 compensa o fim de curso emborrachado."));

        models.Add(new PedalModel(
            "fanatec-clubsport-v2", "Fanatec", "ClubSport Pedals V2",
            PedalSensorType.LoadCell, ThreePedals,
            1.05, 1.00, 2.0, 1.0, 0.50,
            new[] { "clubsport pedals v2", "clubsport v2", "csp v2" },
            "Geração anterior do ClubSport: eletrônica mais lenta, pede filtro maior."));

        models.Add(new PedalModel(
            "fanatec-csl-elite-v2", "Fanatec", "CSL Elite Pedals V2",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "csl elite", "elite pedals", "elite v2" },
            "Load Cell de 60 kg no freio. Resposta praticamente linear: curva 1,00."));

        models.Add(new PedalModel(
            "fanatec-csl", "Fanatec", "CSL Pedals",
            PedalSensorType.Potentiometer, TwoPedals,
            PotExponent, 1.00, PotBrakeDeadzone, PotThrottleDeadzone, PotSmoothing,
            new[] { "csl pedals", "csl" },
            "Sai de fábrica com potenciômetro e aceita o kit Load Cell. Sem o kit, vale a curva de potenciômetro."));
    }

    private static void AddMoza(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "moza-sr-p-lite", "Moza Racing", "SR-P Lite",
            PedalSensorType.HallEffect, TwoPedals,
            HallExponent, 1.00, HallBrakeDeadzone, HallThrottleDeadzone, HallSmoothing,
            new[] { "sr p lite", "srp lite" },
            "Sensor Hall no freio: curso linear, sinal limpo e sem Load Cell."));

        models.Add(new PedalModel(
            "moza-sr-p", "Moza Racing", "SR-P",
            PedalSensorType.LoadCell, TwoPedals,
            1.10, 1.00, 1.5, 1.0, 0.55,
            new[] { "sr p", "srp" },
            "Load Cell de 100 kg opcional. A curva 1,10 ajuda a modular a parte final do curso."));

        models.Add(new PedalModel(
            "moza-crp2", "Moza Racing", "CRP2",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "crp2", "crp 2" },
            "Load Cell de 120 kg com haste rígida. Curva linear e filtro alto."));

        models.Add(new PedalModel(
            "moza-crp", "Moza Racing", "CRP",
            PedalSensorType.LoadCell, ThreePedals,
            1.05, 1.00, 1.5, 1.0, 0.50,
            new[] { "crp" },
            "Primeira geração do CRP: célula de carga confiável, sinal um pouco mais ruidoso."));
    }

    private static void AddSimagic(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "simagic-p2000", "Simagic", "P2000",
            PedalSensorType.LoadCell, TwoPedals,
            1.00, 1.00, 1.0, 1.0, 0.60,
            new[] { "p2000" },
            "Freio hidráulico com Load Cell: resposta muito rápida, filtro quase desligado."));

        models.Add(new PedalModel(
            "simagic-p1000", "Simagic", "P1000",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "p1000", "simagic" },
            "Load Cell de 100 kg com haste de alumínio. Curva linear."));
    }

    private static void AddHeusinkveld(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "heusinkveld-ultimate", "Heusinkveld", "Sim Pedals Ultimate+",
            PedalSensorType.LoadCell, ThreePedals,
            0.95, 1.00, 1.0, 1.0, 0.60,
            new[] { "ultimate", "heusinkveld" },
            "Load Cell de até 140 kg. Curva abaixo de 1,00 aproveita a força extra do conjunto."));

        models.Add(new PedalModel(
            "heusinkveld-sprint", "Heusinkveld", "Sim Pedals Sprint",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "sprint" },
            "Load Cell de 65 kg. Sinal limpo: dá para confiar no valor bruto do freio."));
    }

    private static void AddAsetek(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "asetek-invicta", "Asetek SimSports", "Invicta Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            0.95, 1.00, 1.0, 1.0, 0.60,
            new[] { "invicta" },
            "Load Cell de duas etapas (até 200 kg). Curva abaixo de 1,00 mantém a progressão de força."));

        models.Add(new PedalModel(
            "asetek-forte", "Asetek SimSports", "Forte Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "forte" },
            "Load Cell de 100 kg com haste rígida. Perfil linear padrão de Load Cell."));

        models.Add(new PedalModel(
            "asetek-la-prima", "Asetek SimSports", "La Prima Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 2.0, 1.0, 0.50,
            new[] { "la prima", "laprima", "asetek" },
            "Load Cell de entrada da Asetek: curso maior, aceita um pouco mais de zona morta."));
    }

    private static void AddLogitech(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "logitech-g-pro", "Logitech", "G Pro Racing Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            1.10, 1.00, 2.0, 1.5, 0.50,
            new[] { "g pro racing pedals", "g pro pedals", "pro racing pedals" },
            "Load Cell de 100 kg com mola modular. A mola macia pede curva 1,10."));

        models.Add(new PedalModel(
            "logitech-g29", "Logitech", "G25 / G27 / G29 / G920 / G923",
            PedalSensorType.Potentiometer, ThreePedals,
            PotExponent, 1.00, 4.5, PotThrottleDeadzone, 0.30,
            new[] { "g29", "g920", "g923", "g27", "g25", "driving force", "logitech" },
            "Potenciômetro no braço do freio: folga no começo do curso e ruído no fim. Zona morta maior e filtro forte."));
    }

    private static void AddThrustmaster(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "thrustmaster-tlcm", "Thrustmaster", "T-LCM",
            PedalSensorType.LoadCell, ThreePedals,
            1.05, 1.00, 1.5, 1.0, 0.50,
            new[] { "t lcm", "tlcm" },
            "Load Cell de 100 kg com molas trocáveis. Curva 1,05 equilibra as molas mais macias."));

        models.Add(new PedalModel(
            "thrustmaster-t3pa", "Thrustmaster", "T3PA / T3PA-Pro",
            PedalSensorType.Potentiometer, ThreePedals,
            PotExponent, 1.00, PotBrakeDeadzone, PotThrottleDeadzone, PotSmoothing,
            new[] { "t3pa" },
            "Potenciômetro com cone de borracha: a curva ajuda a controlar a parte final do curso."));
    }

    private static void AddCustom(List<PedalModel> models)
    {
        models.Add(new PedalModel(
            "simsonn-pro", "Simsonn", "Pro Pedals (Load Cell)",
            PedalSensorType.LoadCell, ThreePedals,
            1.10, 1.00, 2.0, 1.0, 0.45,
            new[] { "simsonn" },
            "Placa HID própria aparece como controlador genérico: calibre os eixos, mas a Load Cell é rápida."));

        models.Add(new PedalModel(
            "simjack-pro", "SimJack", "Pro / UT Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            1.10, 1.00, 2.5, 1.0, 0.40,
            new[] { "simjack" },
            "Célula de carga genérica com ruído: filtro em 0,40 e zona morta de 2,5% estabilizam a leitura parada."));

        models.Add(new PedalModel(
            "vrs-directforce", "VRS", "DirectForce Pro Pedals",
            PedalSensorType.LoadCell, ThreePedals,
            1.00, 1.00, 1.5, 1.0, 0.55,
            new[] { "directforce", "vrs" },
            "Load Cell de 130 kg. Curva linear, filtro padrão de Load Cell."));

        models.Add(new PedalModel(
            "cammus-lc100", "Cammus", "LC100 Pedals",
            PedalSensorType.LoadCell, TwoPedals,
            1.05, 1.00, 2.0, 1.5, 0.50,
            new[] { "cammus", "lc100" },
            "Load Cell de entrada, curso longo e mola progressiva."));

        models.Add(new PedalModel(
            "generic-load-cell", "Genérico", "Kit DIY / Arduino + Load Cell",
            PedalSensorType.LoadCell, ThreePedals,
            1.10, 1.00, 2.5, 1.5, 0.40,
            new[] { "load cell", "arduino", "stm32", "leonardo" },
            "Controlador montado em casa: nomes de eixo imprevisíveis e muito ruído. Calibre à mão e mantenha o filtro forte."));
    }

    private static IReadOnlyList<string> BuildBrands()
    {
        var brands = new List<string>();

        foreach (PedalModel model in All)
        {
            if (brands.Contains(model.Brand))
            {
                continue;
            }

            brands.Add(model.Brand);
        }

        brands.Sort(StringComparer.OrdinalIgnoreCase);
        return brands;
    }
}
