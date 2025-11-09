using HojaDeRuta.Models.DAO;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DTO
{
    public class HojaFile
    {
        public string? Id { get; set; }
        public string Cliente { get; set; }
        public string Sector { get; set; }
        public string Subarea { get; set; }
        public string Numero { get; set; }

        [Display(Name = "Nombre Genérico")]
        public string NombreGenerico { get; set; }
        public string Descripcion { get; set; }

        [Display(Name = "Fecha de Documento")]
        public DateTime? FechaDocumento { get; set; }

        [Display(Name = "Socio Firmante")]
        public string SocioFirmante { get; set; }
        public string? Sindico { get; set; }

        [Display(Name = "Contrato Plataforma")]
        public string ContratoPlataforma { get; set; }
        public string? Preparo { get; set; }

        [Display(Name = "Preparo Fecha")]
        public string? PreparoFecha { get; set; }
        public string? Reviso { get; set; }

        [Display(Name = "Revisó Fecha")]
        public string? RevisoFecha { get; set; }

        [Display(Name = "Revisó Gerente")]
        public string? RevisionGerente { get; set; }

        [Display(Name = "Revisó Gerente Fecha")]
        public string? RevisionGerenteFecha { get; set; }

        [Display(Name = "Engagement Partner")]
        public string? EngagementPartner { get; set; }

        [Display(Name = "Engagement Fecha")]
        public string? EngagementPartnerFecha { get; set; }

        [Display(Name = "Socio Firmante Fecha")]
        public string? SocioFirmanteFecha { get; set; }

        [Display(Name = "Fecha Limite")]
        public string? FechaLimite { get; set; }

        [Display(Name = "Fecha de Cierre")]
        public DateTime? FechaDeCierre { get; set; }

        [Display(Name = "Gestor Final")]
        public string GestorFinal { get; set; }

        [Display(Name = "Lugar de Firma")]
        public string LugarFirma { get; set; }

        [Display(Name = "Ruta del Documento")]
        public string RutaDoc { get; set; }

        [Display(Name = "Ruta de Papeles")]
        public string RutaPapeles { get; set; }
        public string? Observaciones { get; set; }
        public string? Estado { get; set; }
    }
}
