using HojaDeRuta.Models.Enums;

namespace HojaDeRuta.Models.DTO
{
    public class WorkflowCurrentStage
    {
        public string StageKey { get; set; } = string.Empty;
        public string ReviewerEmployee { get; set; } = string.Empty;
        public Estado StageState { get; set; }
    }
}
