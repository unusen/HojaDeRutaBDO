using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.Enums
{
    public enum Estado
    {
        [Display(Name = "Activa")]
        Activa = 0,

        [Display(Name = "Firmada")]
        Firmada = 1,

        [Display(Name = "Rechazada")]
        Rechazada = 2
    }
}
