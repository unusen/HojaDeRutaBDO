using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.OData_Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;

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
        private static CookieContainer AuthCookie = new CookieContainer();

        public CreatioService(IConfiguration configuration)
        {
            _configuration = configuration;
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


        public List<Account> GetClientesByCreatedOn(DateTime LastSyncDate)
        {
            List<Account> allAccounts = new List<Account>();
            int skip = 0;
            const int top = 40;

            try
            {
                bool hasMoreRecords = true;

                while (hasMoreRecords)
                {
                    string select = "?$select=Id,AlternativeName,BGClienteID,CreatedOn,BGEstado";

                    string odataFormattedDate = LastSyncDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    string where = "&$filter=CreatedOn ge datetime'" + odataFormattedDate + "'" + "and BGEstado eq 'Activo'";
                    string pagination = $"&$top={top}&$skip={skip}";

                    string requestUri = _serverUriUsr + "/AccountCollection" + select + where + pagination;

                    var request = HttpWebRequest.Create(requestUri) as HttpWebRequest;
                    request.Method = "GET";
                    CookieCollection cookieCollection = AuthCookie.GetCookies(new Uri(_authServiceUri));
                    string csrfToken = cookieCollection["BPMCSRF"].Value;
                    request.CookieContainer = AuthCookie;
                    request.Headers.Add("BPMCSRF", csrfToken);
                    request.Headers.Set("ForceUseSession", "true");
                    request.Timeout = 600000;

                    using (var response = request.GetResponse())
                    {
                        if (((HttpWebResponse)response).StatusCode == HttpStatusCode.OK)
                        {
                            XDocument xmlDoc = XDocument.Load(response.GetResponseStream());
                            response.Close();

                            var items = from entry in xmlDoc.Descendants(_atom + "entry")
                                        select new Account()
                                        {
                                            Id = (entry.Element(_atom + "content").Element(_dsmd + "properties").Element(_ds + "Id").Value),
                                            AlternativeName = (entry.Element(_atom + "content").Element(_dsmd + "properties").Element(_ds + "AlternativeName").Value),
                                            BGClienteID = int.Parse(entry.Element(_atom + "content").Element(_dsmd + "properties").Element(_ds + "BGClienteID").Value),
                                            CreatedOn = Convert.ToDateTime(entry.Element(_atom + "content").Element(_dsmd + "properties").Element(_ds + "CreatedOn").Value),
                                            BGEstado = (entry.Element(_atom + "content").Element(_dsmd + "properties").Element(_ds + "BGEstado").Value)
                                        };

                            allAccounts.AddRange(items);

                            if (items.Count() < top)
                            {
                                hasMoreRecords = false;
                            }
                            else
                            {
                                skip += top;
                            }
                        }
                    }
                }

                return allAccounts;
            }
            catch (Exception ex)
            {
                return null;
            }
            finally
            {
                WriteCookiesToDisk(AuthCookie);
            }
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
            catch { }
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
                catch { }

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
            catch { }

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
                Console.WriteLine($"Error al guardar cookies: {ex.Message}");
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
                Console.WriteLine($"Error al leer cookies: {ex.Message}");
                return new CookieContainer();
            }
        }
        #endregion    

    }
}
