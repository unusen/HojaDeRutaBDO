namespace HojaDeRuta.Models.DTO
{
    public class WorkflowValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; set; } = new();
    }
}
