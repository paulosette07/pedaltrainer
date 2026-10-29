namespace PedalTrainer.Core.Input.Pedals;

/// <summary>
/// Tecnologia do sensor do freio. Muda o comportamento esperado do sinal: uma Load Cell
/// mede força (resposta praticamente linear e limpa), um potenciômetro mede curso e
/// costuma exigir mais filtro e uma curva que abra espaço perto do fim do curso.
/// </summary>
public enum PedalSensorType
{
    /// <summary>Sensor de curso resistivo (Logitech G29, Thrustmaster T3PA).</summary>
    Potentiometer,

    /// <summary>Sensor de curso magnético, mais linear e sem desgaste (Moza SR-P Lite).</summary>
    HallEffect,

    /// <summary>Célula de carga: o valor bruto é proporcional à força no pedal.</summary>
    LoadCell
}
