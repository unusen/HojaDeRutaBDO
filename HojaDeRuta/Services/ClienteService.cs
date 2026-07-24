using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;

namespace HojaDeRuta.Services
{
    public class ClienteService
    {
        private readonly IGenericRepository<Clientes> _clientesRepository;
        private readonly ILogger<ClienteService> _logger;

        public ClienteService(IGenericRepository<Clientes> clientesRepository, ILogger<ClienteService> logger)
        {
            _clientesRepository = clientesRepository;
            _logger = logger;
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
                _logger.LogError(ex, "Error al recuperar el listado completo de clientes.");
                throw new Exception("No se pudo cargar el listado de clientes desde la base de datos.", ex);
            }
        }

        public async Task<Clientes> GetClienteById(int Id)
        {
            try
            {
                //string IdCliente = Id.ToString();
                return await _clientesRepository.GetByIdAsync(Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar el cliente con ID {ClienteId}", Id);
                throw new Exception("Error al recuperar la información del cliente.", ex);
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
                _logger.LogError(ex, "Error al crear un nuevo cliente individual.");
                throw new Exception("No se pudo registrar el nuevo cliente.", ex);
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
                _logger.LogError(ex, "Error al crear un rango de clientes (sincronización masiva).");
                throw new Exception("Error al intentar guardar múltiples clientes en la base de datos.", ex);
            }
        }
    }
}
