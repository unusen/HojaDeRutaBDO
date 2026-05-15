using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface IRutaDocumentoService
    {
        Task<string> ResolveNetworkPathAsync(string? ruta);
        Task<FileValidationResult> ValidateAttachmentAsync(string hojaId, string? rutaDoc, string? adjunto);
    }
}
