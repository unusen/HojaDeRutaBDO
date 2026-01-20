namespace HojaDeRuta.Services.LoginService
{
    using DocumentFormat.OpenXml.Spreadsheet;
    using DocumentFormat.OpenXml.Wordprocessing;
    using HojaDeRuta.Models.Config;
    using HojaDeRuta.Models.DAO;
    using HojaDeRuta.Services.Repository;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Microsoft.Graph;
    using Microsoft.Identity.Web;
    using System.Linq.Expressions;

    public class LoginService : ILoginService
    {
        private readonly ILogger<LoginService> _logger;
        private readonly GraphServiceClient _graphClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SharedService _sharedService;
        private readonly IGenericRepository<Revisores> _revisorRepository;
        private readonly GroupsSettings _groupsSettings;
        private readonly DBSettings _dbSettings;

        public LoginService(
            ILogger<LoginService> logger,
            GraphServiceClient graphClient,
            IHttpContextAccessor httpContextAccessor,
            IOptions<GroupsSettings> groupsSettings,
            SharedService sharedService,
            IGenericRepository<Revisores> revisorRepository,
            IOptions<DBSettings> dbSettings
            )
        {
            _logger = logger;
            _graphClient = graphClient;
            _httpContextAccessor = httpContextAccessor;
            _sharedService = sharedService;
            _revisorRepository = revisorRepository;
            _groupsSettings = groupsSettings.Value;
            _dbSettings = dbSettings.Value;
        }       

        public string GetUserName()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;

                var userResult = user?.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                       ?? user?.Identity?.Name
                       ?? string.Empty;

                _logger.LogInformation($"Usuario logueado: {userResult}");

                return userResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                throw new Exception($"No se pudo obtener el nombre del usuario logueado." +
                    $" Consulte al dpto de Sistemas.");
            }            
        }

        //public string GetUserId()
        //{
        //    var user = _httpContextAccessor.HttpContext?.User;

        //    string type = "http://schemas.microsoft.com/identity/claims/objectidentifier";

        //    return user?.Claims.FirstOrDefault(c => c.Type == type)?.Value
        //           ?? user?.Identity?.Name
        //           ?? string.Empty;
        //}

        public string GetUserEmail()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                throw new Exception($"No se pudo obtener el email del usuario logueado." +
                    $" Consulte al dpto de Sistemas.");
            }
        }

        public async Task<string> GetUserAreaAsync()
        {
            try
            {
                _logger.LogInformation($"Busqueda de area en AD para el user " +
                    $" {GetUserName()}");

                var user = await _graphClient.Me
                                         .Request()
                                         .Select(u => new { u.Department })
                                         .GetAsync();

                _logger.LogInformation($"Resultado area AD: {user.Department}");                

                var sector = await _sharedService.GetSectorByDetalle(user.Department);

                if (sector == null)
                {
                    _logger.LogError($"El sector {sector.Nombre} no se encontro en la BD");
                    throw new Exception();                    
                }

                _logger.LogInformation($"Sector encontrado en la BD: {sector.Nombre}");

                return sector.Nombre;
                //return user.Department;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                throw new Exception($"No se pudo obtener el sector del usuario logueado." +
                    $" Consulte al dpto de Sistemas.");
            }
        }

        public async Task<string> GetUserCargoAsync()
        {
            var user = await _graphClient.Me
                                         .Request()
                                         .Select(u => new { u.JobTitle })
                                         .GetAsync();
            return user.JobTitle;
        }

        public async Task<IList<GroupConfig>> GetUserGroupsAsync()
        {
            try
            {
                var userName = GetUserName();

                _logger.LogInformation($"Busqueda de grupos en AD para el user " +
                    $" {userName}");

                var user = _httpContextAccessor.HttpContext?.User;                

                if (user == null) return new List<GroupConfig>();

                var groupIds = _groupsSettings.Groups
               .Select(g => g.GroupId)
               .ToList();

                _logger.LogInformation($"Se encontraron en la config {groupIds.Count} grupos");

                var memberGroups = await _graphClient
                    .Me
                    .CheckMemberGroups(groupIds)
                    .Request()
                    .PostAsync();

                _logger.LogInformation( $"Se encontraron en AD {memberGroups.Count}" +
                    $" grupos pertenecientes a HDR para el user {userName}");

                var userRoles = _groupsSettings.Groups
                .Where(cfg => memberGroups.Contains(cfg.GroupId))
                .ToList();

                foreach (var role in userRoles)
                {
                    _logger.LogInformation(
                        $"Grupo encontrado para el user {userName}: {role.Name}");
                }

                return userRoles;

                //if (!memberGroups.Any())
                //{
                //    return null;
                //}                    

                //// Si pertenece a varios, elegimos el de mayor Nivel
                //var matchedGroup = _groupsSettings.Groups
                //    .Where(g => memberGroups.Contains(g.GroupId))
                //    .OrderByDescending(g => g.Nivel)
                //    .FirstOrDefault();

                //var groups = await _graphClient.Me.MemberOf.Request().GetAsync();

                //_logger.LogInformation($"Se encontraron en AD {groups.Count} grupos" +
                //    $" para el user {userName}");

                //var userRoles = new List<GroupConfig>();
                //foreach (var group in groups)
                //{
                //    _logger.LogInformation($"Busqueda de match para el grupo de AD" +
                //        $" {group.Id}");

                //    if (group is Microsoft.Graph.Group g)
                //    {
                //        var match = _groupsSettings.Groups
                //            .FirstOrDefault(cfg => cfg.GroupId == g.Id);

                //        if (match != null)
                //        {
                //            _logger.LogInformation($"Grupo encontrado para el user {userName}:" +
                //                $" {match.Name}");
                //            userRoles.Add(match);
                //        }
                //        else
                //        {
                //            string mensaje = $"E grupo de AD {group.Id} no tiene match" +
                //                $" asociado en el appsettings";
                //            _logger.LogError(mensaje);
                //        }
                //    }
                //}
                //return userRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al encontrar grupos para el user" +
                    $" {GetUserName()}: {ex.Message}");

                throw new Exception($"No se pudo obtener el grupo del usuario logueado." +
                    $" Consulte al dpto de Sistemas.");
            }
        }

        public async Task SyncUsuariosLogueados(
            string? UserName,
            string? Email,
            string? Area,
            string? Cargo,
            IList<GroupConfig> Roles)
        {
            //TODO: GENERAR SP Y METODO PARA SYNC USUARIOS EN CADA LOGUEO
            var spName = _dbSettings.Sp["SyncUsuariosLogueados"].ToString();
        }
    }

}
