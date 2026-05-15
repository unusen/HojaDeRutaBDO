namespace HojaDeRuta.Models.DTO
{
    public class NotificationStatusSnapshot
    {
        public string JobId { get; set; } = string.Empty;
        public string HojaId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = NotificationStatuses.Pending;
        public List<string> Recipients { get; set; } = new();
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public string? LastError { get; set; }
    }

    public static class NotificationStatuses
    {
        public const string Pending = "pending";
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
