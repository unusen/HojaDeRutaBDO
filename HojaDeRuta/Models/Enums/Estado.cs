using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.Enums
{
    public enum Estado
    {
        [Display(Name = "Preparada")]
        Preparada = 0,

        [Display(Name = "Revisada")]
        Revisada = 1,

        [Display(Name = "Revisada Gte.")]
        RevisadaGte = 2,

        [Display(Name = "Engagement")]
        Engagement = 3,

        [Display(Name = "Firmada")]
        Firmada = 4,

        [Display(Name = "Rechazada")]
        Rechazada = 5
    }
}
