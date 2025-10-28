using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.Enums
{
    public enum Estado
    {
        [Display(Name = "Esperando Aprobacion")]
        EsperandoAprobacion = 0,

        [Display(Name = "Aprobada")]
        Aprobada = 1,

        [Display(Name = "Rechazada")]
        Rechazada = 2
    }
}
