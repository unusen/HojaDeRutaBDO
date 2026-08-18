using HojaDeRuta.Services;
using Xunit;

namespace HojaDeRuta.Tests;

public class ErrorIncidentServiceTests
{
    [Fact]
    public void CreateIncidentId_ReturnsTwelveUppercaseHexCharacters()
    {
        var incidentId = ErrorIncidentService.CreateIncidentId();

        Assert.Matches("^[0-9A-F]{12}$", incidentId);
    }

    [Fact]
    public void Truncate_ReturnsOriginalValue_WhenItFits()
    {
        Assert.Equal("detalle", ErrorIncidentService.Truncate("detalle", 10));
    }

    [Fact]
    public void Truncate_AddsEllipsis_AndRespectsMaximumLength()
    {
        var result = ErrorIncidentService.Truncate("abcdefgh", 5);

        Assert.Equal("abcd\u2026", result);
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void Truncate_HandlesNullValues()
    {
        Assert.Equal(string.Empty, ErrorIncidentService.Truncate(null, ErrorIncidentService.UserMessageMaxLength));
    }
}
