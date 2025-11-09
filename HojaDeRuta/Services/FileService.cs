using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using A = DocumentFormat.OpenXml.Drawing;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using FontSize = DocumentFormat.OpenXml.Wordprocessing.FontSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;



namespace HojaDeRuta.Services
{
    public class FileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly PathSetings _pathSettings;

        public FileService(
            ILogger<FileService> logger,
            IOptions<PathSetings> pathSettings)
        {
            _logger = logger;
            _pathSettings = pathSettings.Value;
        }

        public async Task<byte[]> GetPdfFromHtml(string html)
        {
            //var html = GenerarHtmlDesdeDatos(data);

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new PuppeteerSharp.Media.MarginOptions
                {
                    Top = "1cm",
                    Bottom = "1cm",
                    Left = "1cm",
                    Right = "1cm"
                }
            });

            return pdfBytes;
        }

        public byte[] GetWordFromHoja(HojaFile hoja)
        {
            using var ms = new MemoryStream();

            using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                var titleText = new DocumentFormat.OpenXml.Wordprocessing.Text($"Hoja de Ruta Nº {hoja.Id}");
                titleText.Space = SpaceProcessingModeValues.Preserve;

                var title = new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" },
                            new Color() { Val = "354997" }
                            ),
                        titleText
                    )
                );
                body.Append(title);

                var emptyText = new DocumentFormat.OpenXml.Wordprocessing.Text(" ");
                emptyText.Space = SpaceProcessingModeValues.Preserve;
                body.Append(new Paragraph(new Run(emptyText)));

                var props = hoja.GetType().GetProperties();

                foreach (var prop in props)
                {
                    var displayAttr = prop.GetCustomAttributes(typeof(DisplayAttribute), false)
                    .FirstOrDefault() as DisplayAttribute;                   

                    //var name = prop.Name;
                    var name = displayAttr?.Name ?? prop.Name;

                    var rawValue = prop.GetValue(hoja)?.ToString() ?? "";
                    var value = Regex.Replace(rawValue, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);

                    var paragraph = new Paragraph();

                    var nameText = new DocumentFormat.OpenXml.Wordprocessing.Text($"{name}: ");
                    nameText.Space = SpaceProcessingModeValues.Preserve;

                    var runName = new Run(
                        new RunProperties(new Bold(), new Color() { Val = "354997" }),                        
                        nameText
                    );

                    var valueText = new DocumentFormat.OpenXml.Wordprocessing.Text(value);
                    valueText.Space = SpaceProcessingModeValues.Preserve;

                    var runValue = new Run(valueText);

                    paragraph.Append(runName);
                    paragraph.Append(runValue);

                    body.Append(paragraph);

                    var sectionProps = new SectionProperties(
                        new PageSize() { Width = 11906, Height = 16838 }, // A4
                        new PageMargin()
                        {
                            Top = 567, // 1 cm
                            Bottom = 567,
                            Left = 567,
                            Right = 567
                        }
                    );

                    body.Append(sectionProps);
                }

                mainPart.Document.Save();
            }

            return ms.ToArray();
        }

        public string GetHtmlFromHoja(HojaFile hoja)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head>");
            sb.Append("<style>");
            sb.Append("body { font-family: Arial; padding: 10px; }");
            sb.Append("h1 { color: #354997 !important; text-align: center !important; }");
            sb.Append("table { border-collapse: collapse; width: 100%; }");
            sb.Append("th, td { border: 1px solid #ddd; padding: 4px; text-align: left; }");
            sb.Append("th { color: #354997 !important; }");
            sb.Append("</style>");
            sb.Append("</head><body>");
            sb.Append($"<h1>Hoja de Ruta Nº {hoja.Id}</h1>");
            sb.Append("<table>");

            foreach (var prop in hoja.GetType().GetProperties())
            {
                var displayAttr = prop.GetCustomAttributes(typeof(DisplayAttribute), false)
                      .FirstOrDefault() as DisplayAttribute;
                //var nombreAmigable = displayAttr?.Name ?? prop.Name;

                var nombre = prop.Name;
                var valor = prop.GetValue(hoja)?.ToString() ?? "";
                sb.Append($"<tr><th>{displayAttr?.Name ?? prop.Name}</th><td>{valor}</td></tr>");
            }

            sb.Append("</table>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        public async Task SavePDF(string html, string fileName)
        {
            try
            {
                _logger.LogInformation($"Comienzo del guardado del PDF final {fileName}");

                byte[] pdfBytes = await GetPdfFromHtml(html);
                _logger.LogInformation($"Bytes PDF generados correctamente");

                string pdfFolderPath = _pathSettings.PathBase;
                _logger.LogInformation($"Carpeta destino: {pdfFolderPath}");

                Directory.CreateDirectory(pdfFolderPath);

                string pdfFileName = $"{fileName}.pdf";
                string pdfFullPath = Path.Combine(pdfFolderPath, pdfFileName);
                _logger.LogInformation($"Ruta definitiva: {pdfFullPath}");

                await File.WriteAllBytesAsync(pdfFullPath, pdfBytes);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Error al guardar el PDF en la ruta de sistemas. {ex.Message}");
            }

        }
    }
}
