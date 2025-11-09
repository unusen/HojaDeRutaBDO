using HojaDeRuta.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace HojaDeRuta.Services.Repository
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly HojasDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(HojasDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            try
            {
                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al crear {typeof(T).Name}. {ex.Message}");
            }
        }

        public async Task AddRangeAsync(List<T> entities)
        {
            try
            {
                _context.Set<T>().AddRangeAsync(entities);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new Exception($"Error al agregar multiples entidades {typeof(T).Name}. {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al agregar multiples entidades {typeof(T).Name}. {ex.Message} ");
            }
        }

        public async Task<T> GetByIdAsync(string id)
        {
            try
            {
                return await _dbSet.FindAsync(id);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener {typeof(T).Name}. {ex.Message}");
            }
        }

        public async Task<T> GetByIdAsync(int id)
        {
            try
            {
                return await _dbSet.FindAsync(id);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener {typeof(T).Name}. {ex.Message}");
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                return await _dbSet.ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener la lista de {typeof(T).Name}. {ex.Message}");
            }
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            try
            {
                return await _dbSet.Where(predicate).ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al buscar {typeof(T).Name}. {ex.Message}");
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
            try
            {
                var entityType = _context.Model.FindEntityType(typeof(T));
                var keyProperty = entityType.FindPrimaryKey().Properties.First();

                var idValue = entity.GetType().GetProperty(keyProperty.Name).GetValue(entity);

                var existingEntity = await _dbSet.FindAsync(idValue);

                if (existingEntity != null)
                {
                    _context.Entry(existingEntity).CurrentValues.SetValues(entity);
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al actualizar {typeof(T).Name}. {ex.Message}");
            }
            
           
        }


        //public async Task UpdateAsync(int id, T entity)
        //{
        //    if (entity == null) throw new ArgumentNullException(nameof(entity));

        //    try
        //    {
        //        T? objOld = await _dbSet.FindAsync(entity);

        //        if (objOld != null)
        //        {
        //            _context.Entry(objOld).CurrentValues.SetValues(entity);
        //            await _context.SaveChangesAsync();
        //            return;
        //        }

        //        throw new Exception($"No se encontró el id {id} para la entidad {typeof(T).Name}.");
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"Error al actualizar {typeof(T).Name}.", ex);
        //    }
        //}

        public async Task DeleteAsync(int id)
        {
            try
            {
                T? obj = await _dbSet.FindAsync(id);

                if (obj != null)
                {
                    _context.Remove(obj);
                    await _context.SaveChangesAsync();
                }

                throw new Exception($"No se encontró el id {id} para la entidad {typeof(T).Name}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al eliminar {typeof(T).Name}. {ex.Message}");
            }
        }

        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<TValue>(
            string spName, Dictionary<string, TValue> parameters)
        {
            try
            {
                var sqlParams = parameters?
                    .Select(p => new SqlParameter("@" + p.Key.Trim(), (object)p.Value ?? DBNull.Value))
                    .ToArray()
                    ?? Array.Empty<SqlParameter>();

                string command = $"EXEC {spName}";
                if (sqlParams.Length > 0)
                {
                    command += " " + string.Join(", ", sqlParams.Select(p => p.ParameterName));
                }

                return await _context.Set<T>().FromSqlRaw(command, sqlParams).ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al consultar el SP {spName}. {ex.Message}");
            }
        }

        public async Task<TResult> GetMaxValueAsync<TResult>(
            Expression<Func<T, TResult>> prop)
        {
            return await _context.Set<T>().MaxAsync(prop);
        }


    }
}
