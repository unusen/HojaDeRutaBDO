using HojaDeRuta.Models.Enums;

namespace HojaDeRuta.Models.DTO
{
    public class WorkflowStageDefinition
    {
        public string StageKey { get; set; } = string.Empty;
        public string ReviewerEmployee { get; set; } = string.Empty;
        public int? Level { get; set; }
        public bool RequiresStrictPrecedence { get; set; }
        public Estado StageState { get; set; }
    }
}
