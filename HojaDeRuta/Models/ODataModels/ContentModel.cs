using System.Text.Json.Serialization;

namespace HojaDeRuta.Models.OData_Models
{
    public class ContentModel
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }
    }
}
