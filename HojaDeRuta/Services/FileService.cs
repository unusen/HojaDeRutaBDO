using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
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
using Border = DocumentFormat.OpenXml.Spreadsheet.Border;
using Color = DocumentFormat.OpenXml.Spreadsheet.Color;
using Font = DocumentFormat.OpenXml.Spreadsheet.Font;
using Fonts = DocumentFormat.OpenXml.Spreadsheet.Fonts;
using FontSize = DocumentFormat.OpenXml.Wordprocessing.FontSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;



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
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            using var page = await browser.NewPageAsync();

            // Establecemos el contenido HTML
            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
            });

            // Ajustes para permitir scroll horizontal en caso de muchas columnas
            await page.EvaluateExpressionAsync(@"
        document.body.style.overflowX = 'auto';
        document.body.style.width = 'max-content';
    ");

            // Opciones de PDF mejoradas
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                PrintBackground = true,  // Mantiene los colores de fondo
                Scale = 0.8m,            // Escala para ajustar más contenido en la página
                Landscape = true,        // Usa orientación horizontal
                PreferCSSPageSize = true,
                MarginOptions = new PuppeteerSharp.Media.MarginOptions
                {
                    Top = "0.8cm",
                    Bottom = "0.8cm",
                    Left = "0.8cm",
                    Right = "0.8cm"
                }
            });

            return pdfBytes;
        }

        public byte[] GetWordFromHoja(HojaFile hoja)
        {
            using var ms = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                // ---- Título ----
                var title = new Paragraph(
                    new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "28" },
                            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "354997" }
                        ),
                        new Text($"Hoja de Ruta Nº {hoja.Id}") { Space = SpaceProcessingModeValues.Preserve }
                    )
                );
                body.Append(title);

                body.Append(new Paragraph(new Run(new Text(" ")))); // Espacio

                // ---- Propiedades ----
                var props = hoja.GetType().GetProperties();

                foreach (var prop in props)
                {
                    var displayAttr = prop.GetCustomAttributes(typeof(DisplayAttribute), false)
                                          .FirstOrDefault() as DisplayAttribute;

                    var name = displayAttr?.Name ?? prop.Name;

                    var rawValue = prop.GetValue(hoja)?.ToString() ?? "";
                    var value = Regex.Replace(rawValue, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");

                    var paragraph = new Paragraph();

                    var runName = new Run(
                        new RunProperties(new Bold(), new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "354997" }),
                        new Text($"{name}: ") { Space = SpaceProcessingModeValues.Preserve }
                    );

                    var runValue = new Run(
                        new Text(value) { Space = SpaceProcessingModeValues.Preserve }
                    );

                    paragraph.Append(runName);
                    paragraph.Append(runValue);

                    body.Append(paragraph);
                }

                // ---- SectionProperties (solo uno!) ----
                body.Append(new SectionProperties(
                    new PageSize() { Width = 11906, Height = 16838 },
                    new PageMargin()
                    {
                        Top = 567,
                        Bottom = 567,
                        Left = 567,
                        Right = 567
                    }
                ));

                mainPart.Document.Save();
            }

            return ms.ToArray();
        }


        //public byte[] GetWordFromHoja(HojaFile hoja)
        //{
        //    using var ms = new MemoryStream();

        //    using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        //    {
        //        var mainPart = wordDoc.AddMainDocumentPart();
        //        mainPart.Document = new Document(new Body());
        //        var body = mainPart.Document.Body;

        //        var titleText = new DocumentFormat.OpenXml.Wordprocessing.Text($"Hoja de Ruta Nº {hoja.Id}");
        //        titleText.Space = SpaceProcessingModeValues.Preserve;

        //        var title = new Paragraph(
        //            new ParagraphProperties(
        //                new Justification() { Val = JustificationValues.Center }),
        //            new Run(
        //                new RunProperties(
        //                    new Bold(),
        //                    new FontSize() { Val = "28" },
        //                    new DocumentFormat.OpenXml.Office2013.Word.Color() { Val = "354997" }
        //                    ),
        //                titleText
        //            )
        //        );
        //        body.Append(title);

        //        var emptyText = new DocumentFormat.OpenXml.Wordprocessing.Text(" ");
        //        emptyText.Space = SpaceProcessingModeValues.Preserve;
        //        body.Append(new Paragraph(new Run(emptyText)));

        //        var props = hoja.GetType().GetProperties();

        //        foreach (var prop in props)
        //        {
        //            var displayAttr = prop.GetCustomAttributes(typeof(DisplayAttribute), false)
        //            .FirstOrDefault() as DisplayAttribute;                   

        //            //var name = prop.Name;
        //            var name = displayAttr?.Name ?? prop.Name;

        //            var rawValue = prop.GetValue(hoja)?.ToString() ?? "";
        //            var value = Regex.Replace(rawValue, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);

        //            var paragraph = new Paragraph();

        //            var nameText = new DocumentFormat.OpenXml.Wordprocessing.Text($"{name}: ");
        //            nameText.Space = SpaceProcessingModeValues.Preserve;

        //            var runName = new Run(
        //                new RunProperties(new Bold(), new DocumentFormat.OpenXml.Office2013.Word.Color() { Val = "354997" }),                        
        //                nameText
        //            );

        //            var valueText = new DocumentFormat.OpenXml.Wordprocessing.Text(value);
        //            valueText.Space = SpaceProcessingModeValues.Preserve;

        //            var runValue = new Run(valueText);

        //            paragraph.Append(runName);
        //            paragraph.Append(runValue);

        //            body.Append(paragraph);

        //            var sectionProps = new SectionProperties(
        //                new PageSize() { Width = 11906, Height = 16838 }, // A4
        //                new PageMargin()
        //                {
        //                    Top = 567, // 1 cm
        //                    Bottom = 567,
        //                    Left = 567,
        //                    Right = 567
        //                }
        //            );

        //            body.Append(sectionProps);
        //        }

        //        mainPart.Document.Save();
        //    }

        //    return ms.ToArray();
        //}

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

        //METODO PARA GUARDAR LOS REPORTES EN PDF, POR AHORA NO USADO
        public string GetHtmlFromDynamic(IEnumerable<dynamic> data)
        {
            if (data == null || !data.Any())
                return "<html><body><h4>No hay datos para mostrar</h4></body></html>";

            var sb = new StringBuilder();

            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
        body {
            font-family: Arial, sans-serif;
            font-size: 10px;
            margin: 10px;
        }

        h3 {
            text-align: center;
            margin-bottom: 10px;
        }

        .table-container {
            width: 100%;
            overflow-x: auto; /* 🔹 Evita recorte horizontal */
        }

        table {
            border-collapse: collapse;
            min-width: 150%; /* 🔹 Permite más ancho que la página */
            white-space: nowrap; /* 🔹 Mantiene columnas en una línea */
            font-size: 9px;
        }

        th, td {
            border: 1px solid #ccc;
            padding: 4px 6px;
            text-align: left;
        }

        th {
            background-color: #f2f2f2;
            font-weight: bold;
        }

        tr:nth-child(even) {
            background-color: #fafafa;
        }
    ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<h3>Reporte de Hojas</h3>");
            sb.AppendLine("<div class='table-container'>");
            sb.AppendLine("<table>");

            // Tomar las columnas del primer registro
            var firstRow = (IDictionary<string, object>)data.First();
            var columns = firstRow.Keys.ToList();

            // Cabecera
            sb.AppendLine("<thead><tr>");
            foreach (var col in columns)
                sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(col.ToUpper())}</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            // Filas
            foreach (var item in data)
            {
                var dict = (IDictionary<string, object>)item;
                sb.AppendLine("<tr>");
                foreach (var col in columns)
                {
                    var value = dict[col]?.ToString() ?? "";
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(value)}</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public byte[] GetExcelFromDynamic(IEnumerable<dynamic> data, string titulo)
        {
            if (data == null || !data.Any())
                return Array.Empty<byte>();

            using var ms = new MemoryStream();

            using (var document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(sheetData);

                // Crear hoja
                var sheets = document.WorkbookPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet
                {
                    Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Reporte"
                });

                // Obtener columnas
                var firstRow = (IDictionary<string, object>)data.First();
                var columns = firstRow.Keys.ToList();

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = CreateStylesheet();
                stylesPart.Stylesheet.Save();
                var titleRow = new Row();
                var titleCell = new Cell
                {
                    DataType = CellValues.String,
                    CellValue = new CellValue(titulo),
                    StyleIndex = 1 
                };
                titleRow.Append(titleCell);
                sheetData.Append(titleRow);

                sheetData.Append(new Row());

                var headerRow = new Row();
                foreach (var col in columns)
                {
                    var headerCell = new Cell
                    {
                        DataType = CellValues.String,
                        CellValue = new CellValue(col.ToUpper()),
                        StyleIndex = 2 // estilo de encabezado azul/blanco
                    };
                    headerRow.Append(headerCell);
                }
                sheetData.Append(headerRow);

                foreach (var item in data)
                {
                    var dict = (IDictionary<string, object>)item;
                    var row = new Row();

                    foreach (var col in columns)
                    {
                        var value = dict[col]?.ToString() ?? "";
                        row.Append(CreateCell(value, CellValues.String));
                    }

                    sheetData.Append(row);
                }

                workbookPart.Workbook.Save();
            }

            return ms.ToArray();
        }

        private Cell CreateCell(string text, CellValues dataType, bool bold = false)
        {
            var cell = new Cell
            {
                DataType = new EnumValue<CellValues>(dataType),
                CellValue = new CellValue(text)
            };
            return cell;
        }

        private Stylesheet CreateStylesheet()
        {
            return new Stylesheet(
                new Fonts(
                    new Font(), // 0 - normal
                    new Font(new Bold(), new FontSize() { Val = new StringValue("18D") }), // 1 - título
                    new Font(new Bold(), new Color() { Rgb = "FFFFFFFF" }) // 2 - encabezado blanco
                ),
                new Fills(
                    new Fill(new PatternFill() { PatternType = PatternValues.None }), // 0
                    new Fill(new PatternFill() { PatternType = PatternValues.Gray125 }), // 1
                    new Fill(new PatternFill(new ForegroundColor() { Rgb = "354997" }) { PatternType = PatternValues.Solid }) // 2 - azul
                ),
                new Borders(new Border()), // sin bordes por ahora
                new CellFormats(
                    new CellFormat(), // 0 - default
                    new CellFormat() { FontId = 1, ApplyFont = true }, // 1 - título
                    new CellFormat() { FontId = 2, FillId = 2, ApplyFont = true, ApplyFill = true } // 2 - encabezado
                )
            );
        }





        public async Task SavePDF(string html, string folder, string fileName)
        {
            try
            {
                _logger.LogInformation($"Comienzo del guardado del PDF final {fileName}");

                byte[] pdfBytes = await GetPdfFromHtml(html);
                _logger.LogInformation($"Bytes PDF generados correctamente");

                string pathBase = _pathSettings.PathBase;
                string pdfFolderPath = Path.Combine(pathBase, folder);
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
