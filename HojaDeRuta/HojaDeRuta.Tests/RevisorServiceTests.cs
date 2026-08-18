using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services;
using Xunit;

namespace HojaDeRuta.Tests;

public class RevisorServiceTests
{
    [Fact]
    public void TieneAreaDistintaDelSector_ReturnsFalse_WhenAreasMatchIgnoringCaseAndWhitespace()
    {
        var revisor = new Revisores { Area = " bank " };

        var result = RevisorService.TieneAreaDistintaDelSector(revisor, "BANK");

        Assert.False(result);
    }

    [Fact]
    public void TieneAreaDistintaDelSector_ReturnsTrue_WhenAreasDiffer()
    {
        var revisor = new Revisores { Area = "AUD" };

        var result = RevisorService.TieneAreaDistintaDelSector(revisor, "BANK");

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TieneAreaDistintaDelSector_ReturnsTrue_WhenReviewerAreaIsMissing(string? area)
    {
        var revisor = new Revisores { Area = area };

        var result = RevisorService.TieneAreaDistintaDelSector(revisor, "BANK");

        Assert.True(result);
    }
}
