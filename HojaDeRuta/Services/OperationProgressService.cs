using System.Text.Json;
using HojaDeRuta.Models.DTO;
using Microsoft.Extensions.Caching.Distributed;

namespace HojaDeRuta.Services
{
    public class OperationProgressService : IOperationProgressService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly IDistributedCache _cache;

        public OperationProgressService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task StartAsync(string operationId, string title, IEnumerable<OperationProgressStepSnapshot> steps, string? message = null)
        {
            var snapshot = new OperationProgressSnapshot
            {
                OperationId = operationId,
                Status = OperationProgressStatuses.Running,
                Title = title,
                Message = message ?? "Preparando operación...",
                Steps = steps.Select(step => new OperationProgressStepSnapshot
                {
                    Key = step.Key,
                    Label = step.Label,
                    Status = OperationProgressStatuses.Pending,
                    Detail = step.Detail
                }).ToList()
            };

            await SaveAsync(snapshot);
        }

        public async Task SetStepRunningAsync(string operationId, string stepKey, string? message = null, string? detail = null)
        {
            var snapshot = await GetRequiredSnapshotAsync(operationId);
            var step = GetRequiredStep(snapshot, stepKey);

            step.Status = OperationProgressStatuses.Running;
            step.Detail = detail;
            snapshot.Status = OperationProgressStatuses.Running;

            if (!string.IsNullOrWhiteSpace(message))
            {
                snapshot.Message = message;
            }

            await SaveAsync(snapshot);
        }

        public async Task SetStepCompletedAsync(string operationId, string stepKey, string? detail = null, string? message = null)
        {
            var snapshot = await GetRequiredSnapshotAsync(operationId);
            var step = GetRequiredStep(snapshot, stepKey);

            step.Status = OperationProgressStatuses.Completed;
            step.Detail = detail;

            if (!string.IsNullOrWhiteSpace(message))
            {
                snapshot.Message = message;
            }

            await SaveAsync(snapshot);
        }

        public async Task CompleteAsync(string operationId, string? message = null, string? redirectUrl = null)
        {
            var snapshot = await GetRequiredSnapshotAsync(operationId);

            snapshot.Status = OperationProgressStatuses.Completed;
            snapshot.Message = string.IsNullOrWhiteSpace(message) ? "Operación finalizada." : message;
            snapshot.RedirectUrl = redirectUrl;

            foreach (var step in snapshot.Steps.Where(step => step.Status == OperationProgressStatuses.Running))
            {
                step.Status = OperationProgressStatuses.Completed;
            }

            await SaveAsync(snapshot);
        }

        public async Task FailAsync(string operationId, string stepKey, string message, string? detail = null)
        {
            var snapshot = await GetRequiredSnapshotAsync(operationId);
            var step = GetRequiredStep(snapshot, stepKey);

            step.Status = OperationProgressStatuses.Failed;
            step.Detail = detail ?? message;
            snapshot.Status = OperationProgressStatuses.Failed;
            snapshot.Message = message;

            await SaveAsync(snapshot);
        }

        public async Task<OperationProgressSnapshot?> GetAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return null;
            }

            var json = await _cache.GetStringAsync(GetCacheKey(operationId));
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<OperationProgressSnapshot>(json, SerializerOptions);
        }

        private async Task<OperationProgressSnapshot> GetRequiredSnapshotAsync(string operationId)
        {
            return await GetAsync(operationId)
                ?? throw new InvalidOperationException($"No se encontró progreso para la operación {operationId}.");
        }

        private static OperationProgressStepSnapshot GetRequiredStep(OperationProgressSnapshot snapshot, string stepKey)
        {
            return snapshot.Steps.FirstOrDefault(step => step.Key == stepKey)
                ?? throw new InvalidOperationException($"No se encontró el paso {stepKey} para la operación {snapshot.OperationId}.");
        }

        private async Task SaveAsync(OperationProgressSnapshot snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            await _cache.SetStringAsync(
                GetCacheKey(snapshot.OperationId),
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
        }

        private static string GetCacheKey(string operationId)
        {
            return $"operation-progress:{operationId}";
        }
    }
}
