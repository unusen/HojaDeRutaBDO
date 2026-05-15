using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface IUserContextCacheService
    {
        Task<UserContext> GetCurrentUserAsync(CancellationToken cancellationToken = default);
        string GetLastSource();
    }
}
