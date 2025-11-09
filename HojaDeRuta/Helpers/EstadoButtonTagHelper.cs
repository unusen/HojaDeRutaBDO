using HojaDeRuta.Models.DAO;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace HojaDeRuta.Helpers
{
    [HtmlTargetElement("estado-badge")]
    public class EstadoButtonTagHelper : TagHelper
    {
        [HtmlAttributeName("estados")]
        public IEnumerable<HojaEstado>? Estados { get; set; }

        [HtmlAttributeName("etapa")]
        public string? Etapa { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "span";
            output.TagMode = TagMode.StartTagAndEndTag;

            string texto;
            string clase;
            string icono;

            var estado = Estados?.FirstOrDefault(e => e.Etapa == Etapa);

            if (estado != null)
            {
                switch (estado.Estado)
                {
                    case 0:
                        texto = "Pendiente";
                        clase = "badge bg-warning text-dark";
                        icono = "bi-hourglass-split";
                        break;
                    case 1:
                        texto = "Aprobada";
                        clase = "badge bg-success";
                        icono = "bi bi-check-lg";
                        break;
                    case 2:
                        texto = "Rechazada";
                        clase = "badge bg-danger";
                        icono = "bi bi-x-lg";
                        break;
                    default:
                        texto = "Disponible";
                        clase = "badge bg-secondary";
                        icono = "bi bi-question-circle";
                        break;
                }
            }
            else
            {
                texto = "Disponible";
                clase = "badge bg-secondary";
                icono = "bi bi-question-circle";
            }

            //output.Attributes.SetAttribute("type", "button");
            //output.Attributes.SetAttribute("class", clase);
            //output.Content.SetContent(texto);

            var html = $"<i class=\"bi {icono} me-1\"></i>{texto}";
            output.Attributes.SetAttribute("class", clase);
            output.Content.SetHtmlContent(html);
        }
    }
}
