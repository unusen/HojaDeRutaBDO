using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface IOperationProgressService
    {
        Task StartAsync(string operationId, string title, IEnumerable<OperationProgressStepSnapshot> steps, string? message = null);
        Task SetStepRunningAsync(string operationId, string stepKey, string? message = null, string? detail = null);
        Task SetStepCompletedAsync(string operationId, string stepKey, string? detail = null, string? message = null);
        Task CompleteAsync(string operationId, string? message = null, string? redirectUrl = null);
        Task FailAsync(string operationId, string stepKey, string message, string? detail = null);
        Task<OperationProgressSnapshot?> GetAsync(string operationId);
    }
}
