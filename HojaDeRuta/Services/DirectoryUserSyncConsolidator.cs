using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services;

internal static class DirectoryUserSyncConsolidator
{
    internal static DirectoryUserSyncSnapshot BuildSnapshot(IReadOnlyList<DirectoryUserSyncRecord> users)
    {
        var hdrUsers = new List<DirectoryUserSyncCandidate>();
        var duplicateAccounts = 0;
        var rawHdrCount = 0;

        foreach (var group in users
            .Select((user, index) => new { User = user, Index = index, Mail = DirectoryUserSyncFormatter.NormalizeMail(user.Mail) })
            .GroupBy(item => item.Mail is null
                ? $"id:{item.User.Id?.Trim()}:{item.Index}"
                : $"mail:{item.Mail}", StringComparer.OrdinalIgnoreCase))
        {
            var hdrAccounts = group.Where(item => item.User.HighestGroup is not null).ToList();
            rawHdrCount += hdrAccounts.Count;
            if (hdrAccounts.Count == 0)
            {
                continue;
            }

            var selected = hdrAccounts
                .OrderByDescending(item => HasRequiredData(item.User))
                .ThenByDescending(item => item.User.HighestGroup!.Nivel)
                .ThenBy(item => item.User.Id, StringComparer.OrdinalIgnoreCase)
                .First();
            var distinctIds = group
                .Select(item => item.User.Id?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (group.Count() > 1)
            {
                duplicateAccounts += group.Count() - 1;
            }

            hdrUsers.Add(new DirectoryUserSyncCandidate(selected.User, distinctIds > 1));
        }

        return new DirectoryUserSyncSnapshot(hdrUsers, rawHdrCount, duplicateAccounts);
    }

    internal static IReadOnlyList<DirectoryUserSyncRecord> ConsolidateForSynchronization(IReadOnlyList<DirectoryUserSyncRecord> users) =>
        BuildSnapshot(users).HdrUsers.Select(candidate => candidate.User).ToList();

    private static bool HasRequiredData(DirectoryUserSyncRecord user) =>
        !string.IsNullOrWhiteSpace(user.Id) &&
        !string.IsNullOrWhiteSpace(DirectoryUserSyncFormatter.NormalizeMail(user.Mail)) &&
        !string.IsNullOrWhiteSpace(user.GivenName) &&
        !string.IsNullOrWhiteSpace(user.Surname) &&
        !string.IsNullOrWhiteSpace(user.Department);
}

internal sealed record DirectoryUserSyncCandidate(
    DirectoryUserSyncRecord User,
    bool AllowMailIdentityReassignment);

internal sealed record DirectoryUserSyncSnapshot(
    IReadOnlyList<DirectoryUserSyncCandidate> HdrUsers,
    int RawHdrCount,
    int ConsolidatedDuplicateAccounts);
