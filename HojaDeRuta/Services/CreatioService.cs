using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.OData_Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using System.Globalization;

namespace HojaDeRuta.Services
{
    public class CreatioService
    {
        private readonly string _serviceUrl;
        private readonly string _username;
        private readonly string _password;
        private readonly string _authServiceUri;
        private readonly string _serverUriUsr;
        private readonly XNamespace _ds;
        private readonly XNamespace _dsmd;
        private readonly XNamespace _atom;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreatioService> _logger;
        private static CookieContainer AuthCookie = new CookieContainer();

        public CreatioService(IConfiguration configuration, ILogger<CreatioService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceUrl = _configuration.GetSection("CreatioConnection").Get<Connection>().ServiceUrl;
            _username = _configuration.GetSection("CreatioConnection").Get<Connection>().UserName;
            _password = _configuration.GetSection("CreatioConnection").Get<Connection>().Password;
            _authServiceUri = _configuration.GetSection("CreatioConnection").Get<Connection>().AuthServiceUri;
            _ds = _configuration.GetSection("CreatioConnection").Get<Connection>().XNamespaceDS;
            _dsmd = _configuration.GetSection("CreatioConnection").Get<Connection>().XNamespaceDSMD;
            _atom = _configuration.GetSection("CreatioConnection").Get<Connection>().XNamespaceATOM;
            _serverUriUsr = _configuration.GetSection("CreatioConnection").Get<Connection>().ServerUriUsr;

            GetConnectionBPM();
        }

        public List<Account> GetClientesActivos()
        {
            const string activeFilter = "BGEstado eq 'Activo'";
            return FetchAccounts(activeFilter);
        }

        public Account? GetClienteActivoByCodigoPlataforma(string? codigoPlataforma)
        {
            if (string.IsNullOrWhiteSpace(codigoPlataforma) || !int.TryParse(codigoPlataforma.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bgClienteId))
            {
                return null;
            }

            var matches = FetchAccounts($"BGClienteID eq {bgClienteId} and BGEstado eq 'Activo'", pageSize: 1, stopAfterFirstPage: true);
            return matches.FirstOrDefault();
        }

        private List<Account> FetchAccounts(string filter, int pageSize = 40, bool stopAfterFirstPage = false)
        {
            var allAccounts = new List<Account>();
            var skip = 0;

            try
            {
                bool hasMoreRecords = true;

                while (hasMoreRecords)
                {
                    string requestUri = BuildAccountsRequestUri(filter, pageSize, skip);

                    var request = CreateCreatioRequest(requestUri);

                    using var response = request.GetResponse();
                    if (response is not HttpWebResponse webResponse || webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        break;
                    }

                    var items = ReadAccountsFromResponse(response);
                    allAccounts.AddRange(items);

                    if (stopAfterFirstPage || items.Count < pageSize)
                    {
                        hasMoreRecords = false;
                    }
                    else
                    {
                        skip += pageSize;
                    }
                }

                return allAccounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes de Creatio mediante OData. Filter: {Filter}", filter);
                return new List<Account>();
            }
            finally
            {
                WriteCookiesToDisk(AuthCookie);
            }
        }

        private string BuildAccountsRequestUri(string filter, int pageSize, int skip)
        {
            var encodedFilter = Uri.EscapeDataString(filter);
            return $"{_serverUriUsr}/AccountCollection?$select=Id,AlternativeName,BGClienteID,CreatedOn,BGEstado&$filter={encodedFilter}&$top={pageSize}&$skip={skip}";
        }

        private HttpWebRequest CreateCreatioRequest(string requestUri)
        {
            var request = HttpWebRequest.Create(requestUri) as HttpWebRequest
                ?? throw new InvalidOperationException("No se pudo crear la solicitud hacia Creatio.");

            request.Method = "GET";
            request.CookieContainer = AuthCookie;
            request.Headers.Set("ForceUseSession", "true");
            request.Timeout = 600000;

            CookieCollection cookieCollection = AuthCookie.GetCookies(new Uri(_authServiceUri));
            string? csrfToken = cookieCollection["BPMCSRF"]?.Value;
            if (!string.IsNullOrWhiteSpace(csrfToken))
            {
                request.Headers.Add("BPMCSRF", csrfToken);
            }

            return request;
        }

        private List<Account> ReadAccountsFromResponse(WebResponse response)
        {
            XDocument xmlDoc = XDocument.Load(response.GetResponseStream());

            return (from entry in xmlDoc.Descendants(_atom + "entry")
                    let properties = entry.Element(_atom + "content")?.Element(_dsmd + "properties")
                    where properties != null
                    let bgClienteIdValue = properties.Element(_ds + "BGClienteID")?.Value
                    where int.TryParse(bgClienteIdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    select new Account()
                    {
                        Id = properties.Element(_ds + "Id")?.Value ?? string.Empty,
                        AlternativeName = properties.Element(_ds + "AlternativeName")?.Value ?? string.Empty,
                        BGClienteID = int.Parse(bgClienteIdValue!, CultureInfo.InvariantCulture),
                        CreatedOn = ParseCreatioDate(properties.Element(_ds + "CreatedOn")?.Value),
                        BGEstado = properties.Element(_ds + "BGEstado")?.Value ?? string.Empty
                    }).ToList();
        }

        private static DateTime ParseCreatioDate(string? value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }

        #region Login & Cookies
        public CreatioService()
        {
            GetConnectionBPM();
        }

        private void GetConnectionBPM()
        {
            try
            {

                string file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "cookies.dat");
                AuthCookie = ReadCookiesFromDisk(file);
                LoginBPM();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar establecer conexión inicial con BPM/Creatio.");
            }
        }

        private bool LoginBPM()
        {
            try
            {
                var authRequest = HttpWebRequest.Create(_authServiceUri) as HttpWebRequest;
                authRequest.Method = "POST";
                authRequest.ContentType = "application/json";
                authRequest.CookieContainer = AuthCookie;
                authRequest.Headers.Set("ForceUseSession", "true");

                try
                {

                    CookieCollection cookieCollection = AuthCookie.GetCookies(new Uri(_authServiceUri));
                    string csrfToken = cookieCollection["BPMCSRF"].Value;
                    authRequest.Headers.Add("BPMCSRF", csrfToken);

                }
                catch (Exception ex) 
                {
                    _logger.LogWarning(ex, "No se pudo recuperar el token BPMCSRF de las cookies actuales.");
                }

                string userName = _username;
                string userPassword = _password;

                using (var requestStream = authRequest.GetRequestStream())
                {
                    using (var writer = new StreamWriter(requestStream))
                    {
                        writer.Write(@"{
                    ""UserName"":""" + userName + @""",
                    ""UserPassword"":""" + userPassword + @"""
                    }");
                    }
                }

                BPM_ResponseStatus status = null;
                using (var response = (HttpWebResponse)authRequest.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseText = reader.ReadToEnd();
                        //status = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<BPM_ResponseStatus>(responseText);
                        status = JsonSerializer.Deserialize<BPM_ResponseStatus>(responseText);
                    }

                }

                if (status != null)
                {
                    if (status.Code == 0)
                    {
                        WriteCookiesToDisk(AuthCookie);
                        return true;
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla crítica durante el proceso de Login en Creatio.");
            }

            return false;
        }

        public void WriteCookiesToDisk(CookieContainer cookieJar)
        {
            string file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "cookies.dat");

            try
            {
                var cookies = cookieJar.GetAllCookies();
                string json = JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cookies en el disco: {FilePath}", file);
            }
        }

        public CookieContainer ReadCookiesFromDisk(string file)
        {
            try
            {
                string json = File.ReadAllText(file);
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(json);

                var cookieContainer = new CookieContainer();
                foreach (var cookie in cookies)
                {
                    cookieContainer.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                }

                return cookieContainer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron leer las cookies del disco (o el archivo no existe): {FilePath}", file);
                return new CookieContainer();
            }
        }
        #endregion    

    }
}
