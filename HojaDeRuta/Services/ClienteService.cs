using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;

namespace HojaDeRuta.Services
{
    public class ClienteService
    {
        private readonly IGenericRepository<Clientes> _clientesRepository;

        public ClienteService(IGenericRepository<Clientes> clientesRepository)
        {
            _clientesRepository = clientesRepository;
        }

        public async Task<List<Clientes>> GetClientes()
        {
            try
            {
                IEnumerable<Clientes> clientes = await _clientesRepository.GetAllAsync();
                return clientes.OrderBy(c => c.RazonSocial).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task CreateCliente(Clientes cliente)
        {
            try
            {
                await _clientesRepository.AddAsync(cliente);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task CreateClientes(List<Clientes> clientes)
        {
            try
            {
                await _clientesRepository.AddRangeAsync(clientes);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
