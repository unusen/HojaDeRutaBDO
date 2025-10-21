using HojaDeRuta.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO

{
    public class Hoja
    {
        [Key]
        [Column("id")]
        public string? Id { get; set; }

        [Column("cliente")]
        [Required(ErrorMessage = "El cliente no puede estar vacío.")]
        public int Cliente { get; set; }

        //[Column("ClienteName")]
        //public string? ClienteName { get; set; }

        [Column("sector")]
        [Required(ErrorMessage = "{0} no puede estar vacío.")]
        public string Sector { get; set; }

        [Column("subarea")]
        [Required(ErrorMessage = "{0} no puede estar vacío.")]
        public string Subarea { get; set; }

        [Column("numeracion")]
        public string Numero { get; set; }

        [Column("generico")]
        [Required(ErrorMessage = "Tipo Doc. no puede estar vacío.")]
        public string NombreGenerico { get; set; }

        [Column("descripcion")]
        [Required(ErrorMessage = "{0} no puede estar vacío.")]
        public string Descripcion { get; set; }

        [Column("fecha")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? FechaDocumento { get; set; }
        //public string FechaDocumento { get; set; }

        [Column("revisosocio")]
        [Required(ErrorMessage = "Socio Firmante no puede estar vacío.")]
        public string SocioFirmante { get; set; }

        [NotMapped]
        public bool IsSindico { get; set; }

        [Column("sindica")]
        public string? Sindico { get; set; }

        [NotMapped]
        public string CodCliente { get; set; }

        [Column("contrato")]
        [Required(ErrorMessage = "El cliente debe tener contrato.")]
        public string ContratoPlataforma { get; set; }

        [Column("preparo")]
        public string? Preparo { get; set; }

        [Column("preparo_fecha")]
        public string? PreparoFecha { get; set; }

        [Column("reviso")]
        public string? Reviso { get; set; }

        [Column("reviso_fecha")]
        public string? RevisoFecha { get; set; }

        [Column("revisogte")]
        public string? RevisionGerente { get; set; }

        [Column("revisogte_fecha")]
        public string? RevisionGerenteFecha { get; set; }

        [Column("mailengagement")]
        public string? EngagementPartner { get; set; }

        [Column("mailengagement_fecha")]
        public string? EngagementPartnerFecha { get; set; }

        [Column("revisosocio_fecha")]
        public string? SocioFirmanteFecha { get; set; }

        [Column("fecha_limite")]
        public string? FechaLimite { get; set; }

        [Column("manejador_final")]
        [Required(ErrorMessage = "Gestor final no puede estar vacío.")]
        public string GestorFinal { get; set; }

        [Column("lugar_firma")]
        [Required(ErrorMessage = "Lugar de firma no puede estar vacío.")]
        public string LugarFirma { get; set; }

        [Column("rutadoc")]
        [Required(ErrorMessage = "Debe informar la ruta del documento.")]
        public string RutaDoc { get; set; }

        [Column("rutapapeles")]
        [Required(ErrorMessage = "Debe informar la ruta de papeles.")]
        public string RutaPapeles { get; set; }

        [NotMapped]
        [Required(ErrorMessage = "La carpeta de documento debe tener archivos.")]
        public string Adjuntos { get; set; }

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("estado")]
        public Estado? Estado { get; set; }
    }
}
