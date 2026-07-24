using HojaDeRuta.DBContext;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HojaDeRuta.Services
{
    public class HojaWorkflowService : IHojaWorkflowService
    {
        private static readonly string[] ApprovalStageOrder =
        {
            nameof(Hoja.Reviso),
            nameof(Hoja.RevisionGerente),
            nameof(Hoja.EngagementPartner),
            nameof(Hoja.SocioFirmante)
        };

        private static readonly string[] StrictPrecedenceStages =
        {
            nameof(Hoja.Reviso),
            nameof(Hoja.RevisionGerente),
            nameof(Hoja.EngagementPartner)
        };

        private readonly ILogger<HojaWorkflowService> _logger;
        private readonly HojasDbContext _context;
        private readonly ICatalogCacheService _catalogCacheService;

        public HojaWorkflowService(
            ILogger<HojaWorkflowService> logger,
            HojasDbContext context,
            ICatalogCacheService catalogCacheService)
        {
            _logger = logger;
            _context = context;
            _catalogCacheService = catalogCacheService;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowConfigurationAsync(Hoja hoja, string preparadorActual, bool enforcePreparoCurrentUser)
        {
            var result = new WorkflowValidationResult();

            if (hoja == null)
            {
                result.Errors.Add("No pudimos validar el flujo de revisión de la hoja.");
                return result;
            }

            if (hoja.Estado == (int)Estado.Rechazada)
            {
                result.Errors.Add("La hoja está rechazada y no puede reconfigurarse.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(hoja.Preparo))
            {
                result.Errors.Add("La hoja debe tener un preparador asignado.");
                return result;
            }

            if (enforcePreparoCurrentUser &&
                !string.Equals(hoja.Preparo?.Trim(), preparadorActual?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("El preparador de la hoja debe coincidir con el usuario logueado.");
            }

            var revisores = await _catalogCacheService.GetRevisoresAsync();
            var preparador = FindRevisor(revisores, hoja.Preparo);

            if (preparador == null || !preparador.Cargo.HasValue)
            {
                result.Errors.Add("No pudimos validar el nivel del preparador actual.");
                return result;
            }

            var previousReviewer = preparador;

            foreach (var stageKey in StrictPrecedenceStages)
            {
                var reviewerValue = GetStageReviewerValue(hoja, stageKey);
                if (string.IsNullOrWhiteSpace(reviewerValue))
                {
                    continue;
                }

                var reviewer = FindRevisor(revisores, reviewerValue);
                if (reviewer == null || !reviewer.Cargo.HasValue)
                {
                    result.Errors.Add($"No encontramos un revisor válido para la etapa {GetStageDisplayName(stageKey)}.");
                    continue;
                }

                if (!previousReviewer.Cargo.HasValue || reviewer.Cargo.Value <= previousReviewer.Cargo.Value)
                {
                    result.Errors.Add(
                        $"La etapa {GetStageDisplayName(stageKey)} debe tener un nivel superior a {GetStageDisplayNameForPreviousStage(previousReviewer, hoja, stageKey)}.");
                }

                previousReviewer = reviewer;
            }

            if (string.IsNullOrWhiteSpace(hoja.SocioFirmante))
            {
                result.Errors.Add("La hoja debe tener un socio firmante asignado.");
            }

            if (!string.IsNullOrWhiteSpace(hoja.GestorFinal))
            {
                var gestorFinal = FindRevisor(revisores, hoja.GestorFinal);
                if (gestorFinal == null)
                {
                    result.Errors.Add("No encontramos un gestor final válido.");
                }
            }

            return result;
        }

        public async Task<List<WorkflowStageDefinition>> BuildWorkflowStagesAsync(Hoja hoja)
        {
            var revisores = await _catalogCacheService.GetRevisoresAsync();
            var states = await EnsureStatesLoadedAsync(hoja);
            var stages = new List<WorkflowStageDefinition>();

            foreach (var stageKey in ApprovalStageOrder)
            {
                var reviewerValue = GetStageReviewerValue(hoja, stageKey);
                if (string.IsNullOrWhiteSpace(reviewerValue))
                {
                    continue;
                }

                var reviewer = FindRevisor(revisores, reviewerValue);
                var state = states.FirstOrDefault(s => string.Equals(s.Etapa, stageKey, StringComparison.OrdinalIgnoreCase));
                var workflowReviewerIdentifier = ResolveWorkflowReviewerIdentifier(reviewerValue, reviewer);

                stages.Add(new WorkflowStageDefinition
                {
                    StageKey = stageKey,
                    ReviewerEmployee = workflowReviewerIdentifier,
                    Level = reviewer?.Cargo,
                    RequiresStrictPrecedence = StrictPrecedenceStages.Contains(stageKey, StringComparer.OrdinalIgnoreCase),
                    StageState = ParseEstado(state?.Estado)
                });
            }

            return stages;
        }

        public async Task SyncWorkflowStatesAsync(Hoja hoja)
        {
            var configuredStages = await BuildWorkflowStagesAsync(hoja);
            var existingStates = (await EnsureStatesLoadedAsync(hoja)).ToList();

            foreach (var duplicateGroup in existingStates
                .Where(s => !string.IsNullOrWhiteSpace(s.Etapa))
                .GroupBy(s => s.Etapa!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1))
            {
                foreach (var duplicate in duplicateGroup.Skip(1))
                {
                    _context.Hoja_Estado.Remove(duplicate);
                }
            }

            var configuredStageKeys = configuredStages
                .Select(s => s.StageKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var stage in configuredStages)
            {
                var existing = existingStates.FirstOrDefault(s =>
                    string.Equals(s.Etapa, stage.StageKey, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    _context.Hoja_Estado.Add(new HojaEstado
                    {
                        HojaId = hoja.Id,
                        Etapa = stage.StageKey,
                        Revisor = stage.ReviewerEmployee,
                        Estado = (int)Estado.Pendiente
                    });
                    continue;
                }

                if (!string.Equals(existing.Revisor, stage.ReviewerEmployee, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Revisor = stage.ReviewerEmployee;
                    existing.Estado = (int)Estado.Pendiente;
                    existing.MotivoDeRechazo = null;
                }
            }

            foreach (var obsolete in existingStates.Where(s =>
                !string.IsNullOrWhiteSpace(s.Etapa) &&
                !configuredStageKeys.Contains(s.Etapa)))
            {
                _context.Hoja_Estado.Remove(obsolete);
            }

            await _context.SaveChangesAsync();
            hoja.HojaEstados = await _context.Hoja_Estado
                .Where(s => s.HojaId == hoja.Id)
                .OrderBy(s => s.HojaEstadoId)
                .ToListAsync();

            _logger.LogInformation(
                "Workflow states synchronized for HojaId={HojaId}. ConfiguredStages={ConfiguredStages} PersistedStates={PersistedStates}",
                hoja.Id,
                configuredStages.Count,
                hoja.HojaEstados.Count());
        }

        public async Task<WorkflowCurrentStage?> ResolveCurrentStageAsync(Hoja hoja)
        {
            if (hoja == null || hoja.Estado == (int)Estado.Aprobada || hoja.Estado == (int)Estado.Rechazada)
            {
                return null;
            }

            var stages = await BuildWorkflowStagesAsync(hoja);
            foreach (var stage in stages)
            {
                if (stage.StageState == Estado.Pendiente)
                {
                    return new WorkflowCurrentStage
                    {
                        StageKey = stage.StageKey,
                        ReviewerEmployee = stage.ReviewerEmployee,
                        StageState = stage.StageState
                    };
                }
            }

            return null;
        }

        public async Task<string?> ResolveCurrentHandlerAsync(Hoja hoja)
        {
            var currentStage = await ResolveCurrentStageAsync(hoja);
            return currentStage?.ReviewerEmployee;
        }

        public async Task<bool> CanActOnCurrentStageAsync(Hoja hoja, UserContext currentUser, string? accion)
        {
            var currentStage = await ResolveCurrentStageAsync(hoja);
            if (currentStage == null || currentUser == null)
            {
                return false;
            }

            var isCurrentActor = IdentifierMatches(currentStage.ReviewerEmployee, currentUser);
            if (!isCurrentActor)
            {
                return false;
            }

            var normalizedAction = (accion ?? string.Empty).Trim().ToUpperInvariant();
            return normalizedAction switch
            {
                "FIRMAR" => IdentifierMatches(hoja.SocioFirmante ?? string.Empty, currentUser),
                "APROBAR" => !string.Equals(currentStage.StageKey, nameof(Hoja.SocioFirmante), StringComparison.OrdinalIgnoreCase),
                "RECHAZAR" => true,
                _ => true
            };
        }

        public async Task<List<WorkflowStageDefinition>> GetStagesToCloseOnSignatureAsync(Hoja hoja)
        {
            var stages = await BuildWorkflowStagesAsync(hoja);
            var currentStage = await ResolveCurrentStageAsync(hoja);

            if (currentStage == null)
            {
                return new List<WorkflowStageDefinition>();
            }

            var startIndex = stages.FindIndex(stage =>
                string.Equals(stage.StageKey, currentStage.StageKey, StringComparison.OrdinalIgnoreCase));

            if (startIndex < 0)
            {
                return new List<WorkflowStageDefinition>();
            }

            return stages
                .Skip(startIndex)
                .Where(stage => stage.StageState == Estado.Pendiente)
                .ToList();
        }

        public async Task<List<Revisores>> GetAllowedReviewersForStageAsync(Hoja hoja, string stageKey, string? selectedReviewer = null)
        {
            var revisores = await _catalogCacheService.GetRevisoresAsync();
            var requiredBaseLevel = await GetRequiredBaseLevelForStageAsync(hoja, stageKey, revisores);
            var allowed = revisores
                .Where(r => r.Cargo.HasValue && r.Cargo.Value > requiredBaseLevel)
                .DistinctBy(r => r.Empleado)
                .OrderBy(r => r.Detalle)
                .ToList();

            var retained = FindRevisor(revisores, selectedReviewer);
            if (retained != null && !allowed.Any(r => string.Equals(r.Empleado, retained.Empleado, StringComparison.OrdinalIgnoreCase)))
            {
                allowed.Add(retained);
                allowed = allowed
                    .DistinctBy(r => r.Empleado)
                    .OrderBy(r => r.Detalle)
                    .ToList();
            }

            return allowed;
        }

        private async Task<int> GetRequiredBaseLevelForStageAsync(Hoja hoja, string stageKey, List<Revisores> revisores)
        {
            var previousIdentifiers = stageKey switch
            {
                nameof(Hoja.Reviso) => new[] { hoja.Preparo },
                nameof(Hoja.RevisionGerente) => new[] { hoja.Reviso, hoja.Preparo },
                nameof(Hoja.EngagementPartner) => new[] { hoja.RevisionGerente, hoja.Reviso, hoja.Preparo },
                _ => Array.Empty<string?>()
            };

            foreach (var identifier in previousIdentifiers)
            {
                var previous = FindRevisor(revisores, identifier);
                if (previous?.Cargo.HasValue == true)
                {
                    return previous.Cargo.Value;
                }
            }

            return 0;
        }

        private async Task<List<HojaEstado>> EnsureStatesLoadedAsync(Hoja hoja)
        {
            if (hoja.HojaEstados != null && hoja.HojaEstados.Any())
            {
                return hoja.HojaEstados.ToList();
            }

            var states = await _context.Hoja_Estado
                .Where(s => s.HojaId == hoja.Id)
                .OrderBy(s => s.HojaEstadoId)
                .ToListAsync();

            hoja.HojaEstados = states;
            return states;
        }

        private static Revisores? FindRevisor(IEnumerable<Revisores> revisores, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            var normalized = identifier.Trim();
            return revisores.FirstOrDefault(r =>
                string.Equals(r.Empleado, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Mail, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static string? GetStageReviewerValue(Hoja hoja, string stageKey)
        {
            return stageKey switch
            {
                nameof(Hoja.Reviso) => hoja.Reviso,
                nameof(Hoja.RevisionGerente) => hoja.RevisionGerente,
                nameof(Hoja.EngagementPartner) => hoja.EngagementPartner,
                nameof(Hoja.SocioFirmante) => hoja.SocioFirmante,
                _ => null
            };
        }

        private static string ResolveWorkflowReviewerIdentifier(string reviewerValue, Revisores? reviewer)
        {
            if (!string.IsNullOrWhiteSpace(reviewer?.Empleado))
            {
                return reviewer.Empleado.Trim();
            }

            return reviewerValue?.Trim() ?? string.Empty;
        }

        private static Estado ParseEstado(int? value)
        {
            return value switch
            {
                (int)Estado.Aprobada => Estado.Aprobada,
                (int)Estado.Rechazada => Estado.Rechazada,
                _ => Estado.Pendiente
            };
        }

        private static bool IdentifierMatches(string reviewerIdentifier, UserContext currentUser)
        {
            return string.Equals(reviewerIdentifier?.Trim(), currentUser.Empleado?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reviewerIdentifier?.Trim(), currentUser.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetStageDisplayName(string stageKey)
        {
            return stageKey switch
            {
                nameof(Hoja.Reviso) => "Revisó",
                nameof(Hoja.RevisionGerente) => "Gerente/Dir.",
                nameof(Hoja.EngagementPartner) => "Eng. Partner",
                nameof(Hoja.SocioFirmante) => "Socio firmante",
                _ => stageKey
            };
        }

        private static string GetStageDisplayNameForPreviousStage(Revisores previousReviewer, Hoja hoja, string stageKey)
        {
            if (string.Equals(stageKey, nameof(Hoja.Reviso), StringComparison.OrdinalIgnoreCase))
            {
                return "Preparó";
            }

            if (string.Equals(stageKey, nameof(Hoja.RevisionGerente), StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(hoja.Reviso) ? "Revisó" : "Preparó";
            }

            if (string.Equals(stageKey, nameof(Hoja.EngagementPartner), StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(hoja.RevisionGerente))
                {
                    return "Gerente/Dir.";
                }

                return !string.IsNullOrWhiteSpace(hoja.Reviso) ? "Revisó" : "Preparó";
            }

            return previousReviewer.Detalle ?? "la etapa anterior";
        }
    }
}
