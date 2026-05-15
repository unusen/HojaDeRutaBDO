namespace HojaDeRuta.Models.DTO
{
    public class OperationProgressSnapshot
    {
        public string OperationId { get; set; } = string.Empty;
        public string Status { get; set; } = OperationProgressStatuses.Pending;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RedirectUrl { get; set; }
        public List<OperationProgressStepSnapshot> Steps { get; set; } = new();
    }

    public class OperationProgressStepSnapshot
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = OperationProgressStatuses.Pending;
        public string? Detail { get; set; }
    }

    public static class OperationProgressStatuses
    {
        public const string Pending = "pending";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
