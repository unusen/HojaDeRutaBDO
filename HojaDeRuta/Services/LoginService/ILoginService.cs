using HojaDeRuta.Models.Config;
using Microsoft.Graph;

namespace HojaDeRuta.Services.LoginService
{
    public interface ILoginService
    {
        string GetUserName();
        string GetUserId();
        string GetUserEmail();
        Task<string> GetUserAreaAsync();
        Task<string> GetUserCargoAsync();
        Task<IList<GroupConfig>> GetUserGroupsAsync();
    }
}
