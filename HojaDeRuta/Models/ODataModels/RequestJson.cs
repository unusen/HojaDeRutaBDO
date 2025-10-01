using System.Text.Json.Serialization;

namespace HojaDeRuta.Models.OData_Models
{
    public class RequestJson
    {
        [JsonPropertyName("entitySchema")]
        public string EntitySchema { get; set; }

        [JsonPropertyName("dateSend")]
        public string DateSend { get; set; }

        [JsonPropertyName("content")]
        public ContentModel Content { get; set; }
    }
}
