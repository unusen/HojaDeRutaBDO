using System.Linq.Expressions;

namespace HojaDeRuta.Services.Repository
{
    public interface IGenericRepository<T> where T : class
    {
        public Task AddAsync(T entity);
        Task AddRangeAsync(List<T> entities);
        public Task<T> GetByIdAsync(string id);
        public Task<T> GetByIdAsync(int id);
        public Task<IEnumerable<T>> GetAllAsync();
        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> GetFirstOrLastAsync(Expression<Func<T, bool>> filter, Expression<Func<T, object>> orderBy, bool getLast);
        Task<bool> UpdateAsync(T entity);
        public Task DeleteAsync(int id);
        Task<IEnumerable<T>> ExecuteStoredProcedureAsync<TValue>(string spName, Dictionary<string, TValue> parameters);
        Task<IEnumerable<dynamic>> ExecuteStoredProcedureDynamicAsync(string spName, Dictionary<string, object> parameters);
        Task<TResult> GetMaxValueAsync<TResult>(Expression<Func<T, TResult>> prop);
    }
}
