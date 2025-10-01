namespace HojaDeRuta.Services.LoginService
{
    using HojaDeRuta.Models.Config;
    using HojaDeRuta.Models.DAO;
    using HojaDeRuta.Services.Repository;
    using Microsoft.Extensions.Options;
    using Microsoft.Graph;
    using Microsoft.Identity.Web;
    using System.Linq.Expressions;
    using Microsoft.AspNetCore.Mvc;

    public class LoginService : ILoginService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IGenericRepository<Clientes> _clienteRepository;
        private readonly GroupsSettings _groupsSettings;

        public LoginService(
            GraphServiceClient graphClient,
            ITokenAcquisition tokenAcquisition,
            IHttpContextAccessor httpContextAccessor,
            IOptions<GroupsSettings> groupsSettings,
            IGenericRepository<Clientes> clienteRepository
            )
        {
            _graphClient = graphClient;
            _tokenAcquisition = tokenAcquisition;
            _httpContextAccessor = httpContextAccessor;
            _clienteRepository = clienteRepository;
            _groupsSettings = groupsSettings.Value;
        }

        public async Task<string> GetUserNameAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                   ?? user?.Identity?.Name
                   ?? string.Empty;
        }

        public async Task<string> GetUserEmailAsync()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
        }

        public async Task<string> GetUserAreaAsync()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;

                var area = user?.FindFirst("jobtitle")?.Value ?? string.Empty;
                var cargo = user?.FindFirst("department")?.Value ?? string.Empty;

                //string[] scopes = { "User.Read", "Group.Read.All" };
                //var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

                var user2 = await _graphClient.Me
                                     .Request()
                                     .GetAsync();

                var user1 = await _graphClient.Me
                                         .Request()
                                         .Select(u => new { u.Department, u.JobTitle })
                                         .GetAsync();

                return "";
            }
            catch (MicrosoftIdentityWebChallengeUserException ex)
            {
                throw new Exception(ex.Message);
            }       
          

            //return user?.Claims.FirstOrDefault(c => c.Type == "department")?.Value
            //       ?? user?.Identity?.Name
            //       ?? string.Empty;
        }

        public async Task<string> GetUserCargoAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.Claims.FirstOrDefault(c => c.Type == "jobtitle")?.Value
                   ?? user?.Identity?.Name
                   ?? string.Empty;
        }

        public async Task<IList<GroupConfig>> GetUserGroupsAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return new List<GroupConfig>();

            //string[] scopes = { "User.Read", "Group.Read.All" };
            //var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            var groups = await _graphClient.Me.MemberOf.Request().GetAsync();

            
            var userRoles = new List<GroupConfig>();
            foreach (var group in groups)
            {
                if (group is Microsoft.Graph.Group g)
                {
                    var match = _groupsSettings.Groups
                        .FirstOrDefault(cfg => cfg.GroupId == g.Id);

                    if (match != null)
                    {
                        userRoles.Add(match);
                    }
                }
            }
            return userRoles;
        }      
    }

}
