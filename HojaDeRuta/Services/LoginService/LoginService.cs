namespace HojaDeRuta.Services.LoginService
{
    using HojaDeRuta.Models.Config;
    using HojaDeRuta.Models.DAO;
    using HojaDeRuta.Models.DTO;
    using HojaDeRuta.Services.Repository;
    using Microsoft.Extensions.Options;
    using Microsoft.Graph;
    using System.Diagnostics;

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
            IOptions<DBSettings> dbSettings)
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

                _logger.LogInformation("Usuario logueado: {User}", userResult);
                return userResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar recuperar el nombre del usuario desde el HttpContext.");
                throw new Exception("No se pudo obtener la identidad del usuario logueado. Por favor, reinicie su sesion.", ex);
            }
        }

        public string GetUserEmail()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar recuperar el email del usuario.");
                throw new Exception("No se pudo identificar la direccion de correo del usuario actual.", ex);
            }
        }

        public async Task<string> GetUserAreaAsync()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var userName = GetUserName();
                _logger.LogInformation("Busqueda de area en AD para el user {UserName}", userName);

                var user = await _graphClient.Me
                    .Request()
                    .Select(u => new { u.Department })
                    .GetAsync();

                _logger.LogInformation("Resultado area AD: {Department}", user?.Department ?? "N/A");

                var sector = await _sharedService.GetSectorByDetalle(user?.Department);
                if (sector == null)
                {
                    _logger.LogError("El sector {Department} obtenido del AD no se encontro en la tabla de Sectores de la base de datos.", user?.Department ?? "N/A");
                    throw new Exception($"Su departamento '{user?.Department ?? "Desconocido"}' no esta configurado en el sistema.");
                }

                stopwatch.Stop();
                _logger.LogInformation("Area resuelta para el usuario {UserName}. Sector={Sector} DurationMs={DurationMs}", userName, sector.Nombre, stopwatch.ElapsedMilliseconds);
                return sector.Nombre;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Falla al consultar el departamento/area del usuario en Microsoft Graph.");
                throw new Exception("No se pudo validar su area de trabajo en el Directorio Activo.", ex);
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
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var userName = GetUserName();
                _logger.LogInformation("Busqueda de grupos en AD para el user {UserName}", userName);

                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null)
                {
                    return new List<GroupConfig>();
                }

                var groupIds = _groupsSettings.Groups
                    .Select(group => group.GroupId)
                    .ToList();

                _logger.LogInformation("Se encontraron en la config {Count} grupos", groupIds.Count);

                var memberGroups = await _graphClient
                    .Me
                    .CheckMemberGroups(groupIds)
                    .Request()
                    .PostAsync();

                var userRoles = _groupsSettings.Groups
                    .Where(cfg => memberGroups != null && memberGroups.Contains(cfg.GroupId))
                    .OrderByDescending(cfg => cfg.Nivel)
                    .ToList();

                foreach (var role in userRoles)
                {
                    _logger.LogInformation("Grupo encontrado para el user {UserName}: {GroupName}", userName, role.Name);
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Grupos resueltos para el usuario {UserName}. Count={Count} DurationMs={DurationMs}",
                    userName,
                    userRoles.Count,
                    stopwatch.ElapsedMilliseconds);

                return userRoles;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error critico al consultar grupos de seguridad en Microsoft Graph para el usuario {UserName}", GetUserName());
                throw new Exception("No se pudieron validar sus permisos de acceso en Azure AD.", ex);
            }
        }

        public async Task SyncUsuariosLogueados(UserContext currentUser)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Inicio de SyncUsuariosLogueados");

                if (currentUser == null)
                {
                    throw new ArgumentNullException(nameof(currentUser));
                }

                if (!_dbSettings.Sp.TryGetValue("SyncUsuariosLogueados", out var spName) || string.IsNullOrWhiteSpace(spName))
                {
                    throw new InvalidOperationException("No se encontro configurado DBSettings:Sp:SyncUsuariosLogueados.");
                }

                var highestRole = currentUser.Roles?
                    .OrderByDescending(role => role.Nivel)
                    .FirstOrDefault();

                var parameters = new Dictionary<string, object>
                {
                    { "username", currentUser.UserName ?? string.Empty },
                    { "empleado", currentUser.Empleado ?? string.Empty },
                    { "email", currentUser.Email ?? string.Empty },
                    { "area", currentUser.Area ?? string.Empty },
                    { "nivel", highestRole?.Nivel ?? 0 }
                };

                _logger.LogInformation(
                    "Llamada al SP {StoredProcedure} para el usuario {UserName} con empleado {Empleado}, email {Email}, area {Area} y nivel {Nivel}",
                    spName,
                    currentUser.UserName,
                    currentUser.Empleado,
                    currentUser.Email,
                    currentUser.Area,
                    highestRole?.Nivel ?? 0);

                var result = await _revisorRepository.ExecuteStoredProcedureWithReturnValueAsync(spName, parameters);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Resultado de SyncUsuariosLogueados para {UserName}: {Result} ({ResultDescription}). DurationMs={DurationMs}",
                    currentUser.UserName,
                    result,
                    DescribeSyncUsuariosResult(result),
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error al ejecutar SyncUsuariosLogueados para el usuario {UserName}", currentUser?.UserName);
                throw new Exception("Error al sincronizar la informacion del perfil de usuario.", ex);
            }
        }

        private static string DescribeSyncUsuariosResult(int result)
        {
            return result switch
            {
                1 => "insertado",
                2 => "actualizado",
                0 => "sin cambios",
                _ => "resultado no identificado"
            };
        }
    }
}
