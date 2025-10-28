using HojaDeRuta.Helpers.Validators;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Auditoria
    {
        [Key] 
        public string HojaId { get; set; }


        [Required(ErrorMessage = "Debe completar el Activo.")]
        [ValidateActivo]
        public decimal? Activo { get; set; }


        [Required(ErrorMessage = "Debe completar el Pasivo.")]
        public decimal? Pasivo { get; set; }


        [Required(ErrorMessage = "Debe completar el Patrimonio Neto.")]
        public decimal? PatrimonioNeto { get; set; }


        [Required(ErrorMessage = "Debe completar la Moneda.")]
        public string? Moneda { get; set; }


        [Required(ErrorMessage = "Debe completar la Numeración.")]
        public string? TipoNumeracion { get; set; }


        [Required(ErrorMessage = "Debe completar el Resultado.")]
        public decimal? Resultado { get; set; }


        [Required(ErrorMessage = "Debe completar el Total de ingresos.")]
        public decimal? TotalIngresos { get; set; }


        [Required(ErrorMessage = "Debe completar el Total de otros ingresos.")]
        public decimal? TotalOtrosIngresos { get; set; }
    }
}
