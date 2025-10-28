using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Helpers.Validators
{
    public class ValidateActivo : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            // Obtenemos la instancia del modelo completo
            var instance = validationContext.ObjectInstance;
            var tipo = instance.GetType();

            // Obtenemos los valores de las otras propiedades
            var pasivo = (decimal)tipo.GetProperty("Pasivo").GetValue(instance);
            var patrimonioNeto = (decimal)tipo.GetProperty("PatrimonioNeto").GetValue(instance);
            var activo = (decimal)value;

            if (activo != pasivo + patrimonioNeto)
            {
                return new ValidationResult("El Activo debe ser igual a la suma de Pasivo + Patrimonio Neto.");
            }

            return ValidationResult.Success;
        }
    }
}
