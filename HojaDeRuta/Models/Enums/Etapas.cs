using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.Enums
{
    public enum Etapas
    {
        [Display(Name = "Preparo")]
        Preparo = 0,

        [Display(Name = "Reviso")]
        Reviso = 1,

        [Display(Name = "Reviso Gte.")]
        RevisoGte = 2,

        [Display(Name = "Reviso Engagement")]
        RevisoEngagement = 3,

        [Display(Name = "Reviso Socio")]
        RevisoSocio = 4,
    }
}
