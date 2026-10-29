using System.Linq;
using PedalTrainer.Core.Input.Pedals;

namespace PedalTrainer.Tests;

/// <summary>
/// O catálogo é a porta de entrada do piloto: se um modelo erra o casamento de nome ou
/// traz ajustes fora de faixa, a curva aplicada na calibração sai errada.
/// </summary>
public sealed class PedalModelCatalogTests
{
    [Fact]
    public void Catalog_HasMarketPedalsFromSeveralBrands()
    {
        Assert.True(PedalModelCatalog.All.Count >= 20, "o catálogo deve cobrir os pedais mais vendidos");
        Assert.Contains("Logitech", PedalModelCatalog.Brands);
        Assert.Contains("Fanatec", PedalModelCatalog.Brands);
        Assert.Contains("Moza Racing", PedalModelCatalog.Brands);
        Assert.Contains("Heusinkveld", PedalModelCatalog.Brands);
        Assert.Contains("Asetek SimSports", PedalModelCatalog.Brands);
        Assert.Contains("Thrustmaster", PedalModelCatalog.Brands);
    }

    [Fact]
    public void Catalog_IdentifiersAreUniqueAndSearchable()
    {
        var duplicated = PedalModelCatalog.All
            .GroupBy(model => model.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicated);

        foreach (PedalModel model in PedalModelCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Id));
            Assert.False(string.IsNullOrWhiteSpace(model.Brand));
            Assert.False(string.IsNullOrWhiteSpace(model.Name));
            Assert.NotEmpty(model.MatchKeywords);
            Assert.Same(model, PedalModelCatalog.Find(model.Id));
        }
    }

    [Fact]
    public void Catalog_TuningValuesStayInsideTheCalibrationRange()
    {
        foreach (PedalModel model in PedalModelCatalog.All)
        {
            Assert.InRange(model.RecommendedBrakeExponent, 0.3, 3.0);
            Assert.InRange(model.RecommendedThrottleExponent, 0.3, 3.0);
            Assert.InRange(model.RecommendedBrakeDeadzonePercent, 0.0, 20.0);
            Assert.InRange(model.RecommendedThrottleDeadzonePercent, 0.0, 20.0);
            Assert.InRange(model.RecommendedSmoothingAlpha, 0.1, 1.0);
            Assert.InRange(model.PedalCount, 2, 3);
        }
    }

    [Fact]
    public void Find_UnknownIdentifierFallsBackToCatchAll()
    {
        PedalModel model = PedalModelCatalog.Find("nao-existe");

        Assert.True(model.IsCatchAll);
        Assert.Same(PedalModelCatalog.Unknown, model);
    }

    [Theory]
    [InlineData("Fanatec ClubSport Pedals V3", "fanatec-clubsport-v3")]
    [InlineData("Fanatec CSL Elite Pedals V2", "fanatec-csl-elite-v2")]
    [InlineData("Moza SR-P Lite", "moza-sr-p-lite")]
    [InlineData("Moza CRP2 Pedals", "moza-crp2")]
    [InlineData("Logitech G29 Driving Force Racing Wheel", "logitech-g29")]
    [InlineData("Thrustmaster T-LCM Pedals", "thrustmaster-tlcm")]
    [InlineData("Simsonn Pro Load Cell", "simsonn-pro")]
    [InlineData("Simagic P1000", "simagic-p1000")]
    public void Match_RecognisesHardwareByName(string deviceName, string expectedId)
    {
        PedalModel? model = PedalModelCatalog.Match(deviceName);

        Assert.NotNull(model);
        Assert.Equal(expectedId, model!.Id);
    }

    [Fact]
    public void Match_FallsBackToTheHomebrewProfileForGenericBoards()
    {
        PedalModel? model = PedalModelCatalog.Match("Arduino Micro", "HID-compliant game controller");

        Assert.NotNull(model);
        Assert.Equal("generic-load-cell", model!.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Controlador sem marca")]
    public void Match_ReturnsNullWhenNothingIsRecognised(string? deviceName)
    {
        Assert.Null(PedalModelCatalog.Match(deviceName));
    }

    [Fact]
    public void ForBrand_ReturnsOnlyThatBrand()
    {
        var fanatec = PedalModelCatalog.ForBrand("Fanatec");

        Assert.NotEmpty(fanatec);
        Assert.All(fanatec, model => Assert.Equal("Fanatec", model.Brand));
    }
}
