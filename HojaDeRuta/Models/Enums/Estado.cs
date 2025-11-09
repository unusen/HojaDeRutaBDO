using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.Enums
{
    public enum Estado
    {
        [Display(Name = "Pendiente")]
        Pendiente = 0,

        [Display(Name = "Aprobada")]
        Aprobada = 1,

        [Display(Name = "Rechazada")]
        Rechazada = 2
    }
}
