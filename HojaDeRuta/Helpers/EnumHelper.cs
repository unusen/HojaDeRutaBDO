using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Helpers
{
    public static class EnumHelper
    {
        public static string GetDisplayName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DisplayAttribute)Attribute.GetCustomAttribute(field, typeof(DisplayAttribute));
            return attribute?.Name ?? value.ToString();
        }
    }
}
