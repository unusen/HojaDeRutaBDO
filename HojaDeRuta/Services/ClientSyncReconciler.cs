using System.Globalization;
using HojaDeRuta.Models.DAO;

namespace HojaDeRuta.Services
{
    public sealed class ClientSyncReconciler
    {
        private const double SuspiciousLowWatermarkRatio = 0.5d;
        private const int SuspiciousLowWatermarkMinimumLocalCount = 100;

        public ClientSyncPlan BuildPlan(IEnumerable<Account>? remoteActiveAccounts, IEnumerable<Clientes>? localClients)
        {
            var remoteList = (remoteActiveAccounts ?? Enumerable.Empty<Account>()).ToList();
            var localList = (localClients ?? Enumerable.Empty<Clientes>()).ToList();

            var remoteByCode = remoteList
                .Where(account => account != null && account.BGClienteID > 0)
                .GroupBy(account => NormalizeCode(account.BGClienteID.ToString(CultureInfo.InvariantCulture)))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key!, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var localByCode = localList
                .Where(cliente => cliente != null && !string.IsNullOrWhiteSpace(cliente.CodigoPlataforma))
                .GroupBy(cliente => NormalizeCode(cliente.CodigoPlataforma))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key!,
                    group => group.OrderByDescending(cliente => cliente.Hdr_Activo).ThenBy(cliente => cliente.Id).First(),
                    StringComparer.OrdinalIgnoreCase);

            var activeLocalByCode = localByCode
                .Where(pair => pair.Value.Hdr_Activo)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            var inserts = remoteByCode
                .Where(pair => !localByCode.ContainsKey(pair.Key))
                .Select(pair => new Clientes
                {
                    RazonSocial = pair.Value.AlternativeName,
                    CodigoPlataforma = pair.Key
                })
                .OrderBy(cliente => cliente.CodigoPlataforma, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var deletions = localByCode
                .Where(pair => pair.Value.Hdr_Activo && !remoteByCode.ContainsKey(pair.Key))
                .Select(pair => pair.Value)
                .OrderBy(cliente => cliente.CodigoPlataforma, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var reactivations = remoteByCode
                .Where(pair => localByCode.TryGetValue(pair.Key, out var cliente) && !cliente.Hdr_Activo)
                .Select(pair => localByCode[pair.Key])
                .OrderBy(cliente => cliente.CodigoPlataforma, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ClientSyncPlan
            {
                RemoteActiveCount = remoteByCode.Count,
                LocalCount = activeLocalByCode.Count,
                ClientsToInsert = inserts,
                ClientsToReactivate = reactivations,
                ClientsCandidateToDelete = deletions,
                InvalidRemoteCount = remoteList.Count(account => account == null || account.BGClienteID <= 0),
                InvalidLocalCount = localList.Count(cliente => cliente == null || string.IsNullOrWhiteSpace(cliente.CodigoPlataforma))
            };
        }

        public bool ShouldBlockDeletions(int remoteActiveCount, int localCount)
        {
            if (remoteActiveCount <= 0)
            {
                return true;
            }

            if (localCount < SuspiciousLowWatermarkMinimumLocalCount)
            {
                return false;
            }

            return remoteActiveCount < (int)Math.Ceiling(localCount * SuspiciousLowWatermarkRatio);
        }

        private static string? NormalizeCode(string? code)
        {
            return string.IsNullOrWhiteSpace(code)
                ? null
                : code.Trim();
        }
    }

    public sealed class ClientSyncPlan
    {
        public int RemoteActiveCount { get; init; }
        public int LocalCount { get; init; }
        public int InvalidRemoteCount { get; init; }
        public int InvalidLocalCount { get; init; }
        public List<Clientes> ClientsToInsert { get; init; } = new();
        public List<Clientes> ClientsToReactivate { get; init; } = new();
        public List<Clientes> ClientsCandidateToDelete { get; init; } = new();
    }
}
