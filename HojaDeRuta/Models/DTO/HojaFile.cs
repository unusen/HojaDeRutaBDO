using HojaDeRuta.Models.DAO;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DTO
{
    public class HojaFile
    {
        [Column("id")]
        public string? Id { get; set; }

        [Column("cliente")]
        public string? Cliente { get; set; }

        [Column("sector")]
        public string? Sector { get; set; }

        [Column("subarea")]
        public string? Subarea { get; set; }

        [Column("numeracion")]
        public string? Numero { get; set; }

        [Column("generico")]

        [Display(Name = "Nombre Genérico")]
        public string? NombreGenerico { get; set; }

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("fecha")]

        [Display(Name = "Fecha de Documento")]
        public DateTime? FechaDocumento { get; set; }

        [Column("revisosocio")]

        [Display(Name = "Socio Firmante")]
        public string? SocioFirmante { get; set; }

        [Column("sindica")]
        public string? Sindico { get; set; }

        [Column("contrato")]

        [Display(Name = "contrato")]
        public string? ContratoPlataforma { get; set; }

        [Column("preparo")]
        public string? Preparo { get; set; }

        [Column("preparo_fecha")]

        [Display(Name = "Preparo Fecha")]
        public string? PreparoFecha { get; set; }

        [Column("reviso")]
        public string? Reviso { get; set; }

        [Column("reviso_fecha")]

        [Display(Name = "Revisó Fecha")]
        public string? RevisoFecha { get; set; }

        [Column("revisogte")]

        [Display(Name = "Revisó Gerente")]
        public string? RevisionGerente { get; set; }

        [Column("revisogte_fecha")]

        [Display(Name = "Revisó Gerente Fecha")]
        public string? RevisionGerenteFecha { get; set; }

        [Column("revisoengagement")]

        [Display(Name = "Engagement Partner")]
        public string? EngagementPartner { get; set; }

        [Column("revisoengagement_fecha")]

        [Display(Name = "Engagement Fecha")]
        public string? EngagementPartnerFecha { get; set; }

        [Column("revisosocio_fecha")]

        [Display(Name = "Socio Firmante Fecha")]
        public string? SocioFirmanteFecha { get; set; }

        [Column("fecha_limite")]

        [Display(Name = "Fecha Limite")]
        public string? FechaLimite { get; set; }

        [Column("fecha_cierre")]

        [Display(Name = "Fecha de Cierre")]
        public DateTime? FechaDeCierre { get; set; }

        [Column("manejador_final")]

        [Display(Name = "Gestor Final")]
        public string? GestorFinal { get; set; }

        [Column("lugar_firma")]

        [Display(Name = "Lugar de Firma")]
        public string? LugarFirma { get; set; }

        [Column("rutadoc")]

        [Display(Name = "Ruta del Documento")]
        public string? RutaDoc { get; set; }

        [Column("rutapapeles")]

        [Display(Name = "Ruta de Papeles")]
        public string? RutaPapeles { get; set; }

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }
    }
}
