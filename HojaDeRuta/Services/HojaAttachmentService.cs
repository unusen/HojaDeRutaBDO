using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class HojaAttachmentService : IHojaAttachmentService
    {
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
        private readonly FileService _fileService;
        private readonly ILogger<HojaAttachmentService> _logger;
        private readonly PathSetings _pathSettings;
        private readonly IRutaDocumentoService _rutaDocumentoService;

        public HojaAttachmentService(
            FileService fileService,
            IRutaDocumentoService rutaDocumentoService,
            IOptions<PathSetings> pathSettings,
            ILogger<HojaAttachmentService> logger)
        {
            _fileService = fileService;
            _rutaDocumentoService = rutaDocumentoService;
            _pathSettings = pathSettings.Value;
            _logger = logger;
        }

        public FileStorageMode GetConfiguredMode()
        {
            if (Enum.TryParse<FileStorageMode>(_pathSettings.FileStorageMode, true, out var mode))
            {
                return mode;
            }

            return FileStorageMode.Hybrid;
        }

        public FileStorageMode ResolveMode(Hoja hoja)
        {
            var configuredMode = GetConfiguredMode();

            return configuredMode switch
            {
                FileStorageMode.SharedFolder => FileStorageMode.SharedFolder,
                FileStorageMode.AppStorage => FileStorageMode.AppStorage,
                _ => !string.IsNullOrWhiteSpace(hoja.ArchivoTemp)
                    ? FileStorageMode.AppStorage
                    : FileStorageMode.SharedFolder
            };
        }

        public IReadOnlyList<HojaArchivoDescriptor> GetAttachments(Hoja hoja)
        {
            var primary = GetPrimaryAttachment(hoja);
            if (primary == null)
            {
                return Array.Empty<HojaArchivoDescriptor>();
            }

            return new[] { primary };
        }

        public HojaArchivoDescriptor? GetPrimaryAttachment(Hoja hoja)
        {
            if (hoja == null || string.IsNullOrWhiteSpace(hoja.Adjuntos))
            {
                return null;
            }

            var mode = ResolveMode(hoja);
            var origin = mode == FileStorageMode.SharedFolder
                ? HojaArchivoOrigen.SharedFolder
                : HojaArchivoOrigen.AppStorage;

            return new HojaArchivoDescriptor
            {
                HojaId = hoja.Id,
                NombreOriginal = hoja.Adjuntos,
                NombreStorage = hoja.ArchivoTemp,
                Hash = hoja.ArchivoHash,
                ContentType = GetContentType(hoja.Adjuntos),
                Origen = origin,
                EsPrincipal = true,
                RutaFuente = mode == FileStorageMode.SharedFolder ? hoja.RutaDoc : _fileService.GetTempFilePath(hoja.ArchivoTemp ?? string.Empty)
            };
        }

        public async Task PreparePrimaryAttachmentAsync(Hoja hoja, Hoja? existingHoja, IFormFile? uploadedFile)
        {
            PreserveExistingMetadata(hoja, existingHoja);

            if (!HasMeaningfulUpload(uploadedFile))
            {
                _logger.LogInformation(
                    "Adjunto principal: se conserva metadata existente. Hoja={HojaId} TieneAdjunto={HasAttachment} ArchivoTemp={ArchivoTemp}",
                    hoja?.Id ?? BuildFallbackHojaId(hoja),
                    !string.IsNullOrWhiteSpace(hoja?.Adjuntos),
                    hoja?.ArchivoTemp ?? "(null)");
                return;
            }

            hoja.Adjuntos = Path.GetFileName(uploadedFile.FileName);

            if (GetConfiguredMode() == FileStorageMode.SharedFolder)
            {
                _logger.LogInformation(
                    "Adjunto principal: modo SharedFolder, se actualiza solo metadata visual. Hoja={HojaId} Archivo={FileName}",
                    hoja?.Id ?? BuildFallbackHojaId(hoja),
                    hoja.Adjuntos);
                return;
            }

            var hojaId = !string.IsNullOrWhiteSpace(hoja.Id)
                ? hoja.Id
                : BuildFallbackHojaId(hoja);

            var (fileName, hash) = await _fileService.SaveToTempAsync(uploadedFile, hojaId);
            hoja.ArchivoTemp = fileName;
            hoja.ArchivoHash = hash;

            _logger.LogInformation(
                "Adjunto principal: archivo reemplazado en AppStorage. Hoja={HojaId} Archivo={FileName} Temp={TempFile}",
                hojaId,
                hoja.Adjuntos,
                hoja.ArchivoTemp);
        }

        public async Task<FileValidationResult> ValidatePrimaryAttachmentAsync(Hoja hoja)
        {
            if (hoja == null)
            {
                return new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "No pudimos identificar la hoja asociada al archivo."
                };
            }

            if (string.IsNullOrWhiteSpace(hoja.Adjuntos))
            {
                return new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "Todavía faltan datos del documento adjunto para poder validarlo."
                };
            }

            if (ResolveMode(hoja) == FileStorageMode.SharedFolder)
            {
                return await _rutaDocumentoService.ValidateAttachmentAsync(hoja.Id ?? string.Empty, hoja.RutaDoc, hoja.Adjuntos);
            }

            if (string.IsNullOrWhiteSpace(hoja.ArchivoTemp) || !_fileService.TempFileExists(hoja.ArchivoTemp))
            {
                return new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "No encontramos el archivo cargado en la app. Volvé a adjuntarlo antes de continuar."
                };
            }

            if (!string.IsNullOrWhiteSpace(hoja.ArchivoHash)
                && !await _fileService.VerifyHashAsync(hoja.ArchivoTemp, hoja.ArchivoHash))
            {
                return new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "El archivo cargado cambió o se dañó. Volvé a adjuntarlo para continuar."
                };
            }

            return new FileValidationResult
            {
                Success = true,
                Severity = "success",
                Message = "Archivo disponible en la app."
            };
        }

        public async Task<HojaAttachmentOpenResult> GetOpenResultAsync(Hoja hoja)
        {
            var validation = await ValidatePrimaryAttachmentAsync(hoja);
            if (!validation.Success)
            {
                return new HojaAttachmentOpenResult
                {
                    Success = false,
                    Message = validation.Message
                };
            }

            if (ResolveMode(hoja) == FileStorageMode.SharedFolder)
            {
                var physicalPath = await ResolveSharedFolderPathAsync(hoja);
                return new HojaAttachmentOpenResult
                {
                    Success = true,
                    PhysicalPath = physicalPath,
                    FileName = hoja.Adjuntos ?? Path.GetFileName(physicalPath),
                    ContentType = GetContentType(hoja.Adjuntos ?? physicalPath)
                };
            }

            return new HojaAttachmentOpenResult
            {
                Success = true,
                PhysicalPath = _fileService.GetTempFilePath(hoja.ArchivoTemp!),
                FileName = hoja.Adjuntos ?? hoja.ArchivoTemp ?? "adjunto",
                ContentType = GetContentType(hoja.Adjuntos ?? hoja.ArchivoTemp ?? string.Empty)
            };
        }

        public async Task<HojaAttachmentFinalizeResult> FinalizeSignatureAsync(Hoja hoja, string targetFolder)
        {
            var validation = await ValidatePrimaryAttachmentAsync(hoja);
            if (!validation.Success)
            {
                return new HojaAttachmentFinalizeResult
                {
                    Success = false,
                    Message = validation.Message,
                    Origen = ResolveMode(hoja) == FileStorageMode.SharedFolder
                        ? HojaArchivoOrigen.SharedFolder
                        : HojaArchivoOrigen.AppStorage
                };
            }

            try
            {
                if (ResolveMode(hoja) == FileStorageMode.SharedFolder)
                {
                    var sourcePath = await ResolveSharedFolderPathAsync(hoja);
                    var destinationPath = await _fileService.CopyFileToFinalAsync(
                        sourcePath,
                        targetFolder,
                        hoja.Adjuntos ?? Path.GetFileName(sourcePath));

                    return new HojaAttachmentFinalizeResult
                    {
                        Success = true,
                        FinalPath = destinationPath,
                        FileName = Path.GetFileName(destinationPath),
                        Origen = HojaArchivoOrigen.SharedFolder,
                        Message = "Documento copiado desde carpeta compartida."
                    };
                }

                var finalPath = await _fileService.CopyTempFileToFinalAsync(
                    hoja.ArchivoTemp!,
                    targetFolder,
                    Path.GetFileNameWithoutExtension(hoja.Adjuntos));

                return new HojaAttachmentFinalizeResult
                {
                    Success = true,
                    FinalPath = finalPath,
                    FileName = Path.GetFileName(finalPath),
                    Origen = HojaArchivoOrigen.AppStorage,
                    Message = "Documento copiado desde almacenamiento interno."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar el adjunto de la hoja {HojaId}", hoja.Id);
                return new HojaAttachmentFinalizeResult
                {
                    Success = false,
                    Message = "No se pudo guardar el archivo final en el destino configurado.",
                    Origen = ResolveMode(hoja) == FileStorageMode.SharedFolder
                        ? HojaArchivoOrigen.SharedFolder
                        : HojaArchivoOrigen.AppStorage
                };
            }
        }

        private void PreserveExistingMetadata(Hoja hoja, Hoja? existingHoja)
        {
            if (existingHoja == null)
            {
                return;
            }

            hoja.Adjuntos = existingHoja.Adjuntos;
            hoja.ArchivoTemp = existingHoja.ArchivoTemp;
            hoja.ArchivoHash = existingHoja.ArchivoHash;
        }

        private async Task<string> ResolveSharedFolderPathAsync(Hoja hoja)
        {
            var resolvedFolder = await _rutaDocumentoService.ResolveNetworkPathAsync(hoja.RutaDoc);
            var fileName = hoja.Adjuntos ?? throw new InvalidOperationException("No se encontró nombre de adjunto.");
            return Path.Combine(resolvedFolder, fileName);
        }

        private string GetContentType(string fileName)
        {
            if (_contentTypeProvider.TryGetContentType(fileName, out var contentType))
            {
                return contentType;
            }

            return "application/octet-stream";
        }

        private static string BuildFallbackHojaId(Hoja hoja)
        {
            var sector = string.IsNullOrWhiteSpace(hoja.Sector) ? "HDR" : hoja.Sector.Trim();
            var numero = string.IsNullOrWhiteSpace(hoja.Numero) ? Guid.NewGuid().ToString("N") : hoja.Numero.Trim();
            return $"{sector}_{numero}";
        }

        private static bool HasMeaningfulUpload(IFormFile? uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length <= 0)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(uploadedFile.FileName);
        }
    }
}
