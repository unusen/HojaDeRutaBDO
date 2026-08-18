using HojaDeRuta.DBContext;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HojaDeRuta.Tests
{
    public class HojaWorkflowServiceTests
    {
        [Fact]
        public async Task ValidateWorkflowConfiguration_AllowsOptionalManagerWhenEngagementIsAboveLastSelectedReviewer()
        {
            var service = CreateService();
            var hoja = CreateHoja();
            hoja.Reviso = "revisor6";
            hoja.EngagementPartner = "engagement8";

            var result = await service.ValidateWorkflowConfigurationAsync(hoja, "preparador", true);

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateWorkflowConfiguration_RejectsEqualOrDescendingStrictReviewerLevels()
        {
            var service = CreateService();
            var hoja = CreateHoja();
            hoja.Reviso = "revisor6";
            hoja.RevisionGerente = "gerente6";

            var result = await service.ValidateWorkflowConfigurationAsync(hoja, "preparador", true);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("Gerente/Dir."));
        }

        [Fact]
        public async Task ValidateWorkflowConfiguration_DoesNotApplyPrecedenceToSignerOrFinalManager()
        {
            var service = CreateService();
            var hoja = CreateHoja();
            hoja.Reviso = "revisor6";
            hoja.SocioFirmante = "socioNivel3";
            hoja.GestorFinal = "gestorNivel3";

            var result = await service.ValidateWorkflowConfigurationAsync(hoja, "preparador", true);

            Assert.True(result.IsValid);
        }

        private static HojaWorkflowService CreateService()
        {
            var options = new DbContextOptionsBuilder<HojasDbContext>().Options;
            var context = new HojasDbContext(options);
            var revisores = new List<Revisores>
            {
                new() { Empleado = "preparador", Detalle = "Preparador", Cargo = 5 },
                new() { Empleado = "revisor6", Detalle = "Revisor", Cargo = 6 },
                new() { Empleado = "gerente6", Detalle = "Gerente", Cargo = 6 },
                new() { Empleado = "engagement8", Detalle = "Engagement", Cargo = 8 },
                new() { Empleado = "socioNivel3", Detalle = "Socio", Cargo = 3 },
                new() { Empleado = "gestorNivel3", Detalle = "Gestor", Cargo = 3 }
            };

            return new HojaWorkflowService(
                NullLogger<HojaWorkflowService>.Instance,
                context,
                new CatalogCacheStub(revisores));
        }

        private static Hoja CreateHoja() => new()
        {
            Preparo = "preparador",
            SocioFirmante = "socioNivel3"
        };

        private sealed class CatalogCacheStub(List<Revisores> revisores) : ICatalogCacheService
        {
            public Task<List<Clientes>> GetClientesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Clientes>());
            public Task InvalidateClientesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<List<Socios>> GetSociosAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Socios>());
            public Task<List<Revisores>> GetRevisoresAsync(CancellationToken cancellationToken = default) => Task.FromResult(revisores);
            public Task InvalidateUsuariosAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<List<TipoDocumento>> GetTipoDocumentosAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<TipoDocumento>());
            public Task<List<Sector>> GetSectoresAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Sector>());
            public Task<List<SubArea>> GetSubAreasAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<SubArea>());
            public Task<List<Jurisdiccion>> GetJurisdiccionesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Jurisdiccion>());
            public Task<List<Rutas>> GetRutasAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Rutas>());
            public Task<List<Contratos>> GetContratosByCodigoPlataformaAsync(string? codigoPlataforma, CancellationToken cancellationToken = default) => Task.FromResult(new List<Contratos>());
            public Task InvalidateContratosAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
