using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace HojaDeRuta.Services
{
    public interface IHojaAttachmentService
    {
        FileStorageMode GetConfiguredMode();
        FileStorageMode ResolveMode(Hoja hoja);
        IReadOnlyList<HojaArchivoDescriptor> GetAttachments(Hoja hoja);
        HojaArchivoDescriptor? GetPrimaryAttachment(Hoja hoja);
        Task PreparePrimaryAttachmentAsync(Hoja hoja, Hoja? existingHoja, IFormFile? uploadedFile);
        Task<FileValidationResult> ValidatePrimaryAttachmentAsync(Hoja hoja);
        Task<HojaAttachmentOpenResult> GetOpenResultAsync(Hoja hoja);
        Task<HojaAttachmentFinalizeResult> FinalizeSignatureAsync(Hoja hoja, string targetFolder);
    }
}
