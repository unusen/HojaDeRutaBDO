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

            _logger.LogInformation(
                "Creatio configuration loaded. ServiceUrl={ServiceUrl}; AuthUrl={AuthUrl}; ODataUrl={ODataUrl}; UserConfigured={UserConfigured}; PasswordConfigured={PasswordConfigured}",
                DescribeUri(_serviceUrl),
                DescribeUri(_authServiceUri),
                DescribeUri(_serverUriUsr),
                !string.IsNullOrWhiteSpace(_username),
                !string.IsNullOrWhiteSpace(_password));

            GetConnectionBPM();
        }

        public List<Account> GetClientesSincronizables(string? codigoPlataforma = null)
        {
            if (codigoPlataforma is null)
            {
                return FetchAccounts(BuildClientesSincronizablesFilter());
            }

            if (!int.TryParse(codigoPlataforma.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bgClienteId))
            {
                return new List<Account>();
            }

            return FetchAccounts(BuildClientesSincronizablesFilter(bgClienteId), pageSize: 1, stopAfterFirstPage: true);
        }

        internal static string BuildClientesSincronizablesFilter(int? bgClienteId = null)
        {
            const string baseFilter = "BGClienteID ne null and BGClienteID ne 0 and (BGEstado eq 'Activo' or BGEstado eq null or BGEstado eq '')";
            return bgClienteId.HasValue
                ? $"{baseFilter} and BGClienteID eq {bgClienteId.Value.ToString(CultureInfo.InvariantCulture)}"
                : baseFilter;
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

                    _logger.LogInformation(
                        "Creatio OData request starting. Uri={RequestUri}; PageSize={PageSize}; Skip={Skip}; CookieState={CookieState}",
                        DescribeUri(requestUri),
                        pageSize,
                        skip,
                        DescribeCookies(AuthCookie, requestUri));

                    var request = CreateCreatioRequest(requestUri);

                    using var response = request.GetResponse();
                    if (response is not HttpWebResponse webResponse)
                    {
                        _logger.LogWarning(
                            "Creatio OData returned a non-HTTP response. Uri={RequestUri}; ResponseType={ResponseType}",
                            DescribeUri(requestUri),
                            response.GetType().FullName);
                        break;
                    }

                    if (webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        _logger.LogWarning(
                            "Creatio OData returned an unexpected response. Uri={RequestUri}; StatusCode={StatusCode}; ResponseUri={ResponseUri}",
                            DescribeUri(requestUri),
                            webResponse.StatusCode,
                            DescribeUri(webResponse.ResponseUri?.ToString()));
                        break;
                    }

                    _logger.LogInformation(
                        "Creatio OData response received. Uri={RequestUri}; StatusCode={StatusCode}; ContentType={ContentType}; ContentLength={ContentLength}",
                        DescribeUri(requestUri),
                        webResponse.StatusCode,
                        webResponse.ContentType,
                        webResponse.ContentLength);

                    var items = ReadAccountsFromResponse(response);
                    allAccounts.AddRange(items);

                    _logger.LogInformation(
                        "Creatio OData page processed. Uri={RequestUri}; ItemsInPage={ItemsInPage}; AccumulatedItems={AccumulatedItems}",
                        DescribeUri(requestUri),
                        items.Count,
                        allAccounts.Count);

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
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                _logger.LogError(
                    ex,
                    "Creatio OData HTTP error. Filter={Filter}; StatusCode={StatusCode}; ResponseUri={ResponseUri}; CookieState={CookieState}; ResponseBody={ResponseBody}",
                    filter,
                    errorResponse.StatusCode,
                    DescribeUri(errorResponse.ResponseUri?.ToString()),
                    DescribeCookies(AuthCookie, errorResponse.ResponseUri?.ToString()),
                    ReadResponseBodyForLog(errorResponse));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes de Creatio mediante OData. Filter: {Filter}", filter);
                throw;
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

            _logger.LogInformation(
                "Creatio OData request prepared. Uri={RequestUri}; AuthCookieState={AuthCookieState}; RequestCookieState={RequestCookieState}; HasBpmCsrf={HasBpmCsrf}; HasAspxAuth={HasAspxAuth}; Headers=ForceUseSession:{ForceUseSession},BPMCSRF:{HasBpmCsrf}",
                DescribeUri(requestUri),
                DescribeCookies(AuthCookie, _authServiceUri),
                DescribeCookies(AuthCookie, requestUri),
                !string.IsNullOrWhiteSpace(csrfToken),
                HasCookie(AuthCookie, requestUri, ".ASPXAUTH"),
                request.Headers["ForceUseSession"],
                !string.IsNullOrWhiteSpace(csrfToken));

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
                var cookieFile = new FileInfo(file);
                _logger.LogInformation(
                    "Creatio session initialization starting. CookieFile={CookieFile}; CookieFileExists={CookieFileExists}; CookieFileLength={CookieFileLength}; AuthUrl={AuthUrl}",
                    file,
                    cookieFile.Exists,
                    cookieFile.Exists ? cookieFile.Length : 0,
                    DescribeUri(_authServiceUri));

                AuthCookie = ReadCookiesFromDisk(file);
                _logger.LogInformation("Creatio cookies loaded before login. CookieState={CookieState}", DescribeCookies(AuthCookie, _authServiceUri));

                var loginSucceeded = LoginBPM();
                _logger.LogInformation(
                    "Creatio session initialization completed. LoginSucceeded={LoginSucceeded}; CookieState={CookieState}",
                    loginSucceeded,
                    DescribeCookies(AuthCookie, _authServiceUri));

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

                _logger.LogInformation(
                    "Creatio login request starting. AuthUrl={AuthUrl}; UserConfigured={UserConfigured}; PasswordConfigured={PasswordConfigured}; CookieState={CookieState}",
                    DescribeUri(_authServiceUri),
                    !string.IsNullOrWhiteSpace(_username),
                    !string.IsNullOrWhiteSpace(_password),
                    DescribeCookies(AuthCookie, _authServiceUri));

                try
                {

                    CookieCollection cookieCollection = AuthCookie.GetCookies(new Uri(_authServiceUri));
                    string csrfToken = cookieCollection["BPMCSRF"].Value;
                    authRequest.Headers.Add("BPMCSRF", csrfToken);

                    _logger.LogInformation(
                        "Creatio login cookie headers prepared. HasBpmCsrf=True; HasAspxAuth={HasAspxAuth}; CookieState={CookieState}",
                        HasCookie(AuthCookie, _authServiceUri, ".ASPXAUTH"),
                        DescribeCookies(AuthCookie, _authServiceUri));

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
                    _logger.LogInformation(
                        "Creatio login HTTP response received. StatusCode={StatusCode}; ResponseUri={ResponseUri}; ContentType={ContentType}; ContentLength={ContentLength}",
                        response.StatusCode,
                        DescribeUri(response.ResponseUri?.ToString()),
                        response.ContentType,
                        response.ContentLength);

                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseText = reader.ReadToEnd();
                        //status = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<BPM_ResponseStatus>(responseText);
                        status = JsonSerializer.Deserialize<BPM_ResponseStatus>(responseText);
                    }

                }

                if (status != null)
                {
                    _logger.LogInformation(
                        "Creatio login response parsed. Code={Code}; Message={Message}; HasException={HasException}; CookieState={CookieState}",
                        status.Code,
                        TruncateForLog(status.Message),
                        status.Exception != null,
                        DescribeCookies(AuthCookie, _authServiceUri));

                    if (status.Code == 0)
                    {
                        WriteCookiesToDisk(AuthCookie);
                        return true;
                    }

                    _logger.LogWarning(
                        "Creatio login was rejected by the service. Code={Code}; Message={Message}; CookieState={CookieState}",
                        status.Code,
                        TruncateForLog(status.Message),
                        DescribeCookies(AuthCookie, _authServiceUri));
                }
                else
                {
                    _logger.LogWarning("Creatio login returned an empty or unparseable response. CookieState={CookieState}", DescribeCookies(AuthCookie, _authServiceUri));
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                _logger.LogError(
                    ex,
                    "Creatio login HTTP error. StatusCode={StatusCode}; ResponseUri={ResponseUri}; CookieState={CookieState}; ResponseBody={ResponseBody}",
                    errorResponse.StatusCode,
                    DescribeUri(errorResponse.ResponseUri?.ToString()),
                    DescribeCookies(AuthCookie, _authServiceUri),
                    ReadResponseBodyForLog(errorResponse));
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
                _logger.LogInformation("Creatio cookies persisted. CookieFile={CookieFile}; CookieState={CookieState}", file, DescribeCookies(cookieJar));
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
                var cookieFile = new FileInfo(file);
                _logger.LogInformation(
                    "Reading persisted Creatio cookies. CookieFile={CookieFile}; Exists={Exists}; Length={Length}",
                    file,
                    cookieFile.Exists,
                    cookieFile.Exists ? cookieFile.Length : 0);

                string json = File.ReadAllText(file);
                var cookies = JsonSerializer.Deserialize<List<Cookie>>(json) ?? new List<Cookie>();

                var cookieContainer = new CookieContainer();
                foreach (var cookie in cookies)
                {
                    cookieContainer.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                }

                _logger.LogInformation("Persisted Creatio cookies loaded. CookieFile={CookieFile}; CookieState={CookieState}", file, DescribeCookies(cookieContainer));
                return cookieContainer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron leer las cookies del disco (o el archivo no existe): {FilePath}", file);
                return new CookieContainer();
            }
        }

        private static bool HasCookie(CookieContainer cookieJar, string? uriValue, string cookieName)
        {
            if (!Uri.TryCreate(uriValue, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return cookieJar.GetCookies(uri)[cookieName] != null;
        }

        private static string DescribeCookies(CookieContainer cookieJar, string? uriValue = null)
        {
            try
            {
                CookieCollection cookies = Uri.TryCreate(uriValue, UriKind.Absolute, out var uri)
                    ? cookieJar.GetCookies(uri)
                    : cookieJar.GetAllCookies();
                var metadata = cookies.Cast<Cookie>()
                    .Select(cookie => $"{cookie.Name}@{cookie.Domain}{cookie.Path}")
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();

                return $"JarId={cookieJar.GetHashCode()}; Count={cookies.Count}; Cookies=[{string.Join(',', metadata)}]";
            }
            catch (Exception ex)
            {
                return $"Unavailable={ex.GetType().Name}";
            }
        }

        private static string DescribeUri(string? uriValue)
        {
            return Uri.TryCreate(uriValue, UriKind.Absolute, out var uri)
                ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
                : "(missing-or-invalid)";
        }

        private static string ReadResponseBodyForLog(HttpWebResponse response)
        {
            try
            {
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                return TruncateForLog(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return $"(unavailable: {ex.GetType().Name})";
            }
        }

        private static string TruncateForLog(string? value, int maxLength = 500)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(empty)";
            }

            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }
        #endregion    

    }
}
