using HojaDeRuta.Models.Config;

namespace HojaDeRuta.Services.LoginService
{
    public interface ILoginService
    {
        Task<string> GetUserNameAsync();
        Task<string> GetUserEmailAsync();
        Task<string> GetUserAreaAsync();
        Task<string> GetUserCargoAsync();
        Task<IList<GroupConfig>> GetUserGroupsAsync();
    }
}
