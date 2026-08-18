using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services.LoginService
{
    public interface ILoginService
    {
        string GetUserName();
        //string GetUserId();
        string GetUserEmail();
        Task<string> GetUserAreaAsync();
        Task<string> GetUserCargoAsync();
        Task<IList<GroupConfig>> GetUserGroupsAsync();
        Task SyncUsuariosLogueados(UserContext currentUser);
        Task<IReadOnlyList<DirectoryUserSyncRecord>> GetDirectoryUsersForSyncAsync(CancellationToken cancellationToken = default);
    }
}
