using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO;

[Table("ErrorLog")]
public class ErrorLog
{
    [Key]
    public long Id { get; set; }

    [Required, StringLength(12)]
    public string IncidentId { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    [Required, StringLength(80)]
    public string ErrorCode { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserName { get; set; }

    [StringLength(128)]
    public string? HojaId { get; set; }

    [StringLength(64)]
    public string? OperationId { get; set; }

    [Required, StringLength(512)]
    public string Endpoint { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string UserMessage { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string ExceptionMessage { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Fingerprint { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
