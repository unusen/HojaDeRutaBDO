using HojaDeRuta.Services;
using Xunit;

namespace HojaDeRuta.Tests;

public class DirectoryUserSyncFormatterTests
{
    [Theory]
    [InlineData("PDALESSANDRO@bdoargentina.com.ar", "PDALESSANDRO")]
    [InlineData("  p.dalessandro@bdoargentina.com.ar  ", "P.DALESSANDRO")]
    public void NormalizeMail_UsesUppercaseLocalPart(string input, string expected)
    {
        Assert.Equal(expected, DirectoryUserSyncFormatter.NormalizeMail(input));
    }

    [Fact]
    public void BuildDetail_UsesRequiredUppercaseFormat()
    {
        Assert.Equal("PÉREZ, JUAN CARLOS", DirectoryUserSyncFormatter.BuildDetail(" Pérez ", " Juan Carlos "));
    }
}
