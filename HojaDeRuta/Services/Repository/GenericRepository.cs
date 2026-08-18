using System.Data;
using System.Dynamic;
using System.Linq.Expressions;
using HojaDeRuta.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HojaDeRuta.Services.Repository
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly ILogger<GenericRepository<T>> _logger;
        private readonly HojasDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(HojasDbContext context, ILogger<GenericRepository<T>> logger)
        {
            _logger = logger;
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task AddAsync(T entity)
        {
            _logger.LogInformation("Creación de la entidad {EntityName}", typeof(T).Name);

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            try
            {
                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();
                _logger.LogInformation("La entidad {EntityName} se creó correctamente", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear la entidad {EntityName}", typeof(T).Name);
                throw new Exception($"No se pudo crear el registro de {typeof(T).Name} en la base de datos.", ex);
            }
        }

        public async Task AddRangeAsync(List<T> entities)
        {
            _logger.LogInformation("Creación masiva de la entidad {EntityName}. Count={Count}", typeof(T).Name, entities?.Count ?? 0);

            try
            {
                await _context.Set<T>().AddRangeAsync(entities);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Se crearon {Count} registros de la entidad {EntityName}", entities.Count, typeof(T).Name);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error de actualización de base de datos al agregar múltiples entidades {EntityName}", typeof(T).Name);
                throw new Exception($"Error de persistencia al intentar agregar múltiples registros de {typeof(T).Name}.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al agregar múltiples entidades {EntityName}", typeof(T).Name);
                throw new Exception($"Ocurrió un error inesperado al procesar la carga masiva de {typeof(T).Name}.", ex);
            }
        }

        public async Task<T> GetByIdAsync(string id)
        {
            _logger.LogInformation("Búsqueda de la entidad {EntityName} con id {Id}", typeof(T).Name, id);

            try
            {
                return await _dbSet.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la entidad {EntityName} por ID {Id}", typeof(T).Name, id);
                throw new Exception($"No se pudo recuperar la información de {typeof(T).Name}.", ex);
            }
        }

        public async Task<T> GetByIdAsync(int id)
        {
            _logger.LogInformation("Búsqueda de la entidad {EntityName} con id {Id}", typeof(T).Name, id);

            try
            {
                return await _dbSet.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la entidad {EntityName} por ID {Id}", typeof(T).Name, id);
                throw new Exception($"No se pudo recuperar la información de {typeof(T).Name}.", ex);
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            _logger.LogInformation("Obtener todos los registros de la entidad {EntityName}", typeof(T).Name);

            try
            {
                return await _dbSet.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de {EntityName}", typeof(T).Name);
                throw new Exception($"No se pudo obtener el listado de {typeof(T).Name}.", ex);
            }
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            _logger.LogInformation("Find para la entidad {EntityName}", typeof(T).Name);

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            try
            {
                return await _dbSet.Where(predicate).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar Find para la entidad {EntityName}", typeof(T).Name);
                throw new Exception($"Error al filtrar la información de {typeof(T).Name}.", ex);
            }
        }

        public async Task<T> GetFirstOrLastAsync(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, object>> orderBy,
            bool getLast)
        {
            if (getLast)
            {
                return await _dbSet.Where(filter).OrderBy(orderBy).FirstOrDefaultAsync();
            }

            return await _dbSet.Where(filter).OrderByDescending(orderBy).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateAsync(T entity)
        {
            _logger.LogInformation("Actualización de la entidad {EntityName}", typeof(T).Name);

            try
            {
                var entityType = _context.Model.FindEntityType(typeof(T));
                var keyProperty = entityType?.FindPrimaryKey()?.Properties.FirstOrDefault();

                if (keyProperty == null)
                {
                    throw new InvalidOperationException($"No se encontró la clave primaria para la entidad {typeof(T).Name}.");
                }

                var idValue = entity.GetType().GetProperty(keyProperty.Name)?.GetValue(entity);
                var existingEntity = await _dbSet.FindAsync(idValue);

                if (existingEntity != null)
                {
                    _context.Entry(existingEntity).CurrentValues.SetValues(entity);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Se actualizó la entidad {EntityName} con id {Id}", typeof(T).Name, idValue);
                    return true;
                }

                _logger.LogError("No se encontró la entidad {EntityName} con id {Id} para actualizar", typeof(T).Name, idValue);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la entidad {EntityName}", typeof(T).Name);
                throw new Exception($"No se pudieron guardar los cambios de {typeof(T).Name}.", ex);
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                T? obj = await _dbSet.FindAsync(id);

                if (obj != null)
                {
                    _context.Remove(obj);
                    await _context.SaveChangesAsync();
                    return;
                }

                throw new Exception($"No se encontró el id {id} para la entidad {typeof(T).Name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar la entidad {EntityName} con ID {Id}", typeof(T).Name, id);
                throw new Exception($"No se pudo eliminar el registro de {typeof(T).Name}.", ex);
            }
        }

        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<TValue>(
            string spName,
            Dictionary<string, TValue> parameters)
        {
            _logger.LogInformation("Ejecución del SP {SPName}", spName);

            try
            {
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        $"HojasDbContext sin cadena de conexión al ejecutar el SP {spName}. Provider: {_context.Database.ProviderName ?? "(null)"}");
                }

                var dbConnection = _context.Database.GetDbConnection();
                if (string.IsNullOrWhiteSpace(dbConnection.ConnectionString))
                {
                    throw new InvalidOperationException(
                        $"DbConnection sin cadena al ejecutar el SP {spName}. Provider: {_context.Database.ProviderName ?? "(null)"}. ConnectionType: {dbConnection.GetType().FullName}");
                }

                await _context.Database.CanConnectAsync();

                var sqlParams = parameters?
                    .Select(p => new SqlParameter("@" + p.Key.Trim(), (object?)p.Value ?? DBNull.Value))
                    .ToArray()
                    ?? Array.Empty<SqlParameter>();

                var command = $"EXEC {spName}";
                if (sqlParams.Length > 0)
                {
                    command += " " + string.Join(", ", sqlParams.Select(p => $"{p.ParameterName} = {p.ParameterName}"));
                }

                return await _context.Set<T>().FromSqlRaw(command, sqlParams).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar el SP {SPName}", spName);
                throw new Exception($"Ocurrió un error al consultar la base de datos (SP {spName}).", ex);
            }
        }

        public async Task<IEnumerable<dynamic>> ExecuteStoredProcedureDynamicAsync(
            string spName,
            Dictionary<string, object> parameters)
        {
            _logger.LogInformation("Ejecución del SP dynamic {SPName}", spName);

            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = spName;
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        var sqlParam = command.CreateParameter();
                        sqlParam.ParameterName = "@" + param.Key.Trim();
                        sqlParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(sqlParam);
                    }
                }

                var result = new List<ExpandoObject>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = new ExpandoObject() as IDictionary<string, object?>;
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                        }

                        result.Add((ExpandoObject)row);
                    }
                }

                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar el SP dynamic {SPName}", spName);
                throw new Exception($"Ocurrió un error al procesar la consulta dinámica a la base de datos (SP {spName}).", ex);
            }
        }

        public async Task<int> ExecuteStoredProcedureWithReturnValueAsync(
            string spName,
            Dictionary<string, object> parameters)
        {
            _logger.LogInformation("Ejecución del SP {SPName} con retorno", spName);

            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = spName;
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        var sqlParam = command.CreateParameter();
                        sqlParam.ParameterName = "@" + param.Key.Trim();
                        sqlParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(sqlParam);
                    }
                }

                var returnParameter = command.CreateParameter();
                returnParameter.ParameterName = "@ReturnValue";
                returnParameter.Direction = ParameterDirection.ReturnValue;
                returnParameter.DbType = DbType.Int32;
                command.Parameters.Add(returnParameter);

                await command.ExecuteNonQueryAsync();

                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }

                return returnParameter.Value == DBNull.Value
                    ? 0
                    : Convert.ToInt32(returnParameter.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar el SP {SPName} con retorno", spName);
                throw new Exception($"Ocurrió un error al ejecutar el procedimiento almacenado (SP {spName}).", ex);
            }
        }

        public async Task<TResult> GetMaxValueAsync<TResult>(Expression<Func<T, TResult>> prop)
        {
            return await _context.Set<T>().MaxAsync(prop);
        }
    }
}
