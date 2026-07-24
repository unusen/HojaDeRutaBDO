using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services;
using Xunit;

namespace HojaDeRuta.Tests
{
    public class ClientSyncReconcilerTests
    {
        [Fact]
        public void BuildPlan_DetectsNewClientsAndCandidatesToDelete()
        {
            var reconciler = new ClientSyncReconciler();
            var remote = new List<Account>
            {
                new() { BGClienteID = 100, AlternativeName = "Cliente 100", BGEstado = "Activo" },
                new() { BGClienteID = 200, AlternativeName = "Cliente 200", BGEstado = "Activo" }
            };
            var local = new List<Clientes>
            {
                new() { Id = 1, CodigoPlataforma = "100", RazonSocial = "Cliente 100" },
                new() { Id = 2, CodigoPlataforma = "300", RazonSocial = "Cliente 300" }
            };

            var plan = reconciler.BuildPlan(remote, local);

            Assert.Equal(2, plan.RemoteActiveCount);
            Assert.Equal(2, plan.LocalCount);
            Assert.Single(plan.ClientsToInsert);
            Assert.Equal("200", plan.ClientsToInsert[0].CodigoPlataforma);
            Assert.Single(plan.ClientsCandidateToDelete);
            Assert.Equal("300", plan.ClientsCandidateToDelete[0].CodigoPlataforma);
        }

        [Fact]
        public void BuildPlan_IgnoresInvalidCodes()
        {
            var reconciler = new ClientSyncReconciler();
            var remote = new List<Account>
            {
                new() { BGClienteID = 0, AlternativeName = "Invalido", BGEstado = "Activo" },
                new() { BGClienteID = 500, AlternativeName = "Cliente 500", BGEstado = "Activo" }
            };
            var local = new List<Clientes>
            {
                new() { Id = 1, CodigoPlataforma = "", RazonSocial = "Sin codigo" },
                new() { Id = 2, CodigoPlataforma = "500", RazonSocial = "Cliente 500" }
            };

            var plan = reconciler.BuildPlan(remote, local);

            Assert.Equal(1, plan.RemoteActiveCount);
            Assert.Equal(1, plan.LocalCount);
            Assert.Equal(1, plan.InvalidRemoteCount);
            Assert.Equal(1, plan.InvalidLocalCount);
            Assert.Empty(plan.ClientsToInsert);
            Assert.Empty(plan.ClientsCandidateToDelete);
        }

        [Theory]
        [InlineData(0, 1500, true)]
        [InlineData(600, 1500, true)]
        [InlineData(900, 1500, false)]
        [InlineData(40, 80, false)]
        public void ShouldBlockDeletions_AppliesGuardrail(int remoteCount, int localCount, bool expected)
        {
            var reconciler = new ClientSyncReconciler();

            var result = reconciler.ShouldBlockDeletions(remoteCount, localCount);

            Assert.Equal(expected, result);
        }
    }
}
