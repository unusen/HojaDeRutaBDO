using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface IHojaWorkflowService
    {
        Task<WorkflowValidationResult> ValidateWorkflowConfigurationAsync(Hoja hoja, string preparadorActual, bool enforcePreparoCurrentUser);
        Task<List<WorkflowStageDefinition>> BuildWorkflowStagesAsync(Hoja hoja);
        Task SyncWorkflowStatesAsync(Hoja hoja);
        Task<WorkflowCurrentStage?> ResolveCurrentStageAsync(Hoja hoja);
        Task<string?> ResolveCurrentHandlerAsync(Hoja hoja);
        Task<bool> CanActOnCurrentStageAsync(Hoja hoja, UserContext currentUser, string? accion);
        Task<List<WorkflowStageDefinition>> GetStagesToCloseOnSignatureAsync(Hoja hoja);
        Task<List<Revisores>> GetAllowedReviewersForStageAsync(Hoja hoja, string stageKey, string? selectedReviewer = null);
    }
}
