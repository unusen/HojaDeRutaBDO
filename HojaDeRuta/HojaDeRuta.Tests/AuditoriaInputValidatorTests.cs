using HojaDeRuta.Helpers;
using HojaDeRuta.Models.DTO;
using Xunit;

namespace HojaDeRuta.Tests
{
    public class AuditoriaInputValidatorTests
    {
        [Fact]
        public void TryCreate_AcceptsArgentineFormatAndNegativeValues()
        {
            var input = CrearInput();
            input.Activo = "1.234,56";
            input.Pasivo = "1.000,00";
            input.PatrimonioNeto = "234,56";
            input.Resultado = "-1000,5";

            var resultado = AuditoriaInputValidator.TryCreate(input, "A1", out var auditoria, out var errores);

            Assert.True(resultado);
            Assert.Empty(errores);
            Assert.NotNull(auditoria);
            Assert.Equal(1234.56m, auditoria.Activo);
            Assert.Equal(-1000.5m, auditoria.Resultado);
        }

        [Theory]
        [InlineData("1,000.50")]
        [InlineData("1000.50")]
        [InlineData("1.000,500")]
        public void TryCreate_RejectsNonArgentineOrMoreThanTwoDecimals(string activo)
        {
            var input = CrearInput();
            input.Activo = activo;

            var resultado = AuditoriaInputValidator.TryCreate(input, "A1", out _, out var errores);

            Assert.False(resultado);
            Assert.Contains(nameof(input.Activo), errores.Keys);
        }

        [Fact]
        public void TryCreate_RequiresAccountingEquality()
        {
            var input = CrearInput();
            input.Activo = "1.001,00";

            var resultado = AuditoriaInputValidator.TryCreate(input, "A1", out _, out var errores);

            Assert.False(resultado);
            Assert.Contains(nameof(input.Activo), errores.Keys);
        }

        private static AuditoriaInputModel CrearInput()
        {
            return new AuditoriaInputModel
            {
                Activo = "1.000,00",
                Pasivo = "900,00",
                PatrimonioNeto = "100,00",
                Moneda = "ARS",
                TipoNumeracion = "MILES",
                Resultado = "10,00",
                TotalIngresos = "20,00",
                TotalOtrosIngresos = "30,00"
            };
        }
    }
}
