using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HojaDeRuta.Helpers
{
    public static class AuditoriaInputValidator
    {
        private static readonly Regex FormatoImporteArgentino = new(
            @"^-?(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d{1,2})?$",
            RegexOptions.Compiled);

        public static bool TryCreate(
            AuditoriaInputModel? input,
            string hojaId,
            out Auditoria? auditoria,
            out Dictionary<string, string[]> errores,
            bool requerirCarga = false)
        {
            auditoria = null;
            errores = new Dictionary<string, string[]>();

            if (input == null || !input.TieneDatos())
            {
                if (!requerirCarga)
                {
                    return true;
                }

                input ??= new AuditoriaInputModel();
            }

            var activo = ParsearImporte(input.Activo, nameof(input.Activo), "Debe completar el Activo.", errores);
            var pasivo = ParsearImporte(input.Pasivo, nameof(input.Pasivo), "Debe completar el Pasivo.", errores);
            var patrimonioNeto = ParsearImporte(input.PatrimonioNeto, nameof(input.PatrimonioNeto), "Debe completar el Patrimonio Neto.", errores);
            var resultado = ParsearImporte(input.Resultado, nameof(input.Resultado), "Debe completar el Resultado.", errores);
            var totalIngresos = ParsearImporte(input.TotalIngresos, nameof(input.TotalIngresos), "Debe completar el Total de ingresos.", errores);
            var totalOtrosIngresos = ParsearImporte(input.TotalOtrosIngresos, nameof(input.TotalOtrosIngresos), "Debe completar el Total de otros ingresos.", errores);

            if (string.IsNullOrWhiteSpace(input.Moneda))
            {
                errores[nameof(input.Moneda)] = new[] { "Debe completar la Moneda." };
            }

            if (string.IsNullOrWhiteSpace(input.TipoNumeracion))
            {
                errores[nameof(input.TipoNumeracion)] = new[] { "Debe completar la Numeración." };
            }

            if (activo.HasValue && pasivo.HasValue && patrimonioNeto.HasValue
                && activo.Value != pasivo.Value + patrimonioNeto.Value)
            {
                errores[nameof(input.Activo)] = new[] { "El Activo debe ser igual a la suma de Pasivo + Patrimonio Neto." };
            }

            if (errores.Count > 0)
            {
                return false;
            }

            auditoria = new Auditoria
            {
                HojaId = hojaId,
                Activo = activo,
                Pasivo = pasivo,
                PatrimonioNeto = patrimonioNeto,
                Moneda = input.Moneda!.Trim(),
                TipoNumeracion = input.TipoNumeracion!.Trim(),
                Resultado = resultado,
                TotalIngresos = totalIngresos,
                TotalOtrosIngresos = totalOtrosIngresos
            };

            return true;
        }

        private static decimal? ParsearImporte(
            string? valor,
            string campo,
            string mensajeRequerido,
            IDictionary<string, string[]> errores)
        {
            var valorNormalizado = valor?.Trim();
            if (string.IsNullOrWhiteSpace(valorNormalizado))
            {
                errores[campo] = new[] { mensajeRequerido };
                return null;
            }

            if (!FormatoImporteArgentino.IsMatch(valorNormalizado)
                || !decimal.TryParse(
                    valorNormalizado.Replace(".", string.Empty).Replace(',', '.'),
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var importe))
            {
                errores[campo] = new[] { "Ingresá un importe con formato argentino, por ejemplo 1.234,56." };
                return null;
            }

            return importe;
        }
    }
}
