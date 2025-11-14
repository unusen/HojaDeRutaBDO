using System.Dynamic;
using System.Reflection;

namespace HojaDeRuta.Helpers
{
    public class ProjectionHelper
    {
        public static object GetObjectBySelectedColumns<T>(T entidad, string[] campos)
        {
            var dynamicObject = new ExpandoObject() as IDictionary<string, object>;

            foreach (var campo in campos)
            {
                var propiedad = typeof(T).GetProperty(campo,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (propiedad != null && propiedad.CanRead)
                {
                    dynamicObject[campo] = propiedad.GetValue(entidad);
                }
            }

            return dynamicObject;
        }
    }
}
