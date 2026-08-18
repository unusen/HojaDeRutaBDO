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
                new() { BGClienteID = 200, AlternativeName = "Cliente 200", BGEstado = string.Empty },
                new() { BGClienteID = 400, AlternativeName = "Cliente 400", BGEstado = null! }
            };
            var local = new List<Clientes>
            {
                new() { Id = 1, CodigoPlataforma = "100", RazonSocial = "Cliente 100" },
                new() { Id = 2, CodigoPlataforma = "300", RazonSocial = "Cliente 300" }
            };

            var plan = reconciler.BuildPlan(remote, local);

            Assert.Equal(3, plan.RemoteActiveCount);
            Assert.Equal(2, plan.LocalCount);
            Assert.Equal(2, plan.ClientsToInsert.Count);
            Assert.Equal(new[] { "200", "400" }, plan.ClientsToInsert.Select(cliente => cliente.CodigoPlataforma));
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

        [Fact]
        public void BuildClientesSincronizablesFilter_IncludesAllowedStatusesAndNonNullClientId()
        {
            var filter = CreatioService.BuildClientesSincronizablesFilter();

            Assert.Equal("BGClienteID ne null and BGClienteID ne 0 and (BGEstado eq 'Activo' or BGEstado eq null or BGEstado eq '')", filter);
        }

        [Fact]
        public void BuildClientesSincronizablesFilter_WithClientId_AddsSpecificClientCondition()
        {
            var filter = CreatioService.BuildClientesSincronizablesFilter(123);

            Assert.Equal("BGClienteID ne null and BGClienteID ne 0 and (BGEstado eq 'Activo' or BGEstado eq null or BGEstado eq '') and BGClienteID eq 123", filter);
        }

        [Fact]
        public void BuildPlan_ReactivatesRemoteClientAndDoesNotRetryInactiveClientForInactivation()
        {
            var reconciler = new ClientSyncReconciler();
            var remote = new List<Account>
            {
                new() { BGClienteID = 100, AlternativeName = "Cliente reactivado" }
            };
            var local = new List<Clientes>
            {
                new() { Id = 1, CodigoPlataforma = "100", Hdr_Activo = false },
                new() { Id = 2, CodigoPlataforma = "200", Hdr_Activo = false },
                new() { Id = 3, CodigoPlataforma = "300", Hdr_Activo = true }
            };

            var plan = reconciler.BuildPlan(remote, local);

            Assert.Single(plan.ClientsToReactivate);
            Assert.Equal(1, plan.ClientsToReactivate[0].Id);
            Assert.Single(plan.ClientsCandidateToDelete);
            Assert.Equal(3, plan.ClientsCandidateToDelete[0].Id);
            Assert.Equal(1, plan.LocalCount);
        }

        [Fact]
        public void TruncateSyncControlResult_LimitsValueToTwoHundredAndFiftyFiveCharacters()
        {
            var truncated = SyncService.TruncateSyncControlResult(new string('x', 300));

            Assert.Equal(255, truncated.Length);
            Assert.Equal(new string('x', 255), truncated);
        }
    }
}
