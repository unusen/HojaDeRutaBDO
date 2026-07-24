using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Helpers.Validators
{
    public class ValidateActivo : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (validationContext.ObjectInstance == null)
            {
                return ValidationResult.Success;
            }

            var instance = validationContext.ObjectInstance;
            var tipo = instance.GetType();

            var pasivo = tipo.GetProperty("Pasivo")?.GetValue(instance) as decimal?;
            var patrimonioNeto = tipo.GetProperty("PatrimonioNeto")?.GetValue(instance) as decimal?;
            var activo = value as decimal?;

            if (!activo.HasValue || !pasivo.HasValue || !patrimonioNeto.HasValue)
            {
                return ValidationResult.Success;
            }

            if (activo.Value != pasivo.Value + patrimonioNeto.Value)
            {
                return new ValidationResult("El Activo debe ser igual a la suma de Pasivo + Patrimonio Neto.");
            }

            return ValidationResult.Success;
        }
    }
}
