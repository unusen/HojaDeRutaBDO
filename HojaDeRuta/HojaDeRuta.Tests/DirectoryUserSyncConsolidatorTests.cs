using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services;
using Xunit;

namespace HojaDeRuta.Tests;

public class DirectoryUserSyncConsolidatorTests
{
    [Fact]
    public void ConsolidateForSynchronization_WhenDuplicateMailHasHdrAccount_KeepsOnlyHdrAccount()
    {
        var users = new[]
        {
            CreateUser("old-account", "AMENDEZ@bdoargentina.com.ar", null),
            CreateUser("hdr-account", "AMENDEZ@bdoargentina.com.ar", new GroupConfig { Nivel = 8 })
        };

        var result = DirectoryUserSyncConsolidator.ConsolidateForSynchronization(users);

        var user = Assert.Single(result);
        Assert.Equal("hdr-account", user.Id);
        Assert.Equal(8, user.HighestGroup?.Nivel);
    }

    [Fact]
    public void BuildSnapshot_WhenDuplicateMailHasDifferentObjectIds_AllowsSafeMailIdentityReassignment()
    {
        var users = new[]
        {
            CreateUser("old-account", "AMENDEZ@bdoargentina.com.ar", null),
            CreateUser("hdr-account", "AMENDEZ@bdoargentina.com.ar", new GroupConfig { Nivel = 8 })
        };

        var snapshot = DirectoryUserSyncConsolidator.BuildSnapshot(users);

        var candidate = Assert.Single(snapshot.HdrUsers);
        Assert.Equal("hdr-account", candidate.User.Id);
        Assert.True(candidate.AllowMailIdentityReassignment);
        Assert.Equal(1, snapshot.ConsolidatedDuplicateAccounts);
    }

    [Fact]
    public void ConsolidateForSynchronization_WhenSeveralHdrAccountsHaveSameMail_KeepsHighestLevelDeterministically()
    {
        var users = new[]
        {
            CreateUser("level-4", "DQUINTANA@bdoargentina.com.ar", new GroupConfig { Nivel = 4 }),
            CreateUser("level-8", "DQUINTANA@bdoargentina.com.ar", new GroupConfig { Nivel = 8 })
        };

        var result = DirectoryUserSyncConsolidator.ConsolidateForSynchronization(users);

        var user = Assert.Single(result);
        Assert.Equal("level-8", user.Id);
        Assert.Equal(8, user.HighestGroup?.Nivel);
    }

    [Fact]
    public void ConsolidateForSynchronization_PrefersHdrAccountWithRequiredDataOverHigherLevelIncompleteAccount()
    {
        var users = new[]
        {
            new DirectoryUserSyncRecord
            {
                Id = "incomplete-level-10",
                Mail = "JIRIBARREN@bdoargentina.com.ar",
                HighestGroup = new GroupConfig { Nivel = 10 }
            },
            CreateUser("complete-level-6", "JIRIBARREN@bdoargentina.com.ar", new GroupConfig { Nivel = 6 })
        };

        var result = DirectoryUserSyncConsolidator.ConsolidateForSynchronization(users);

        var user = Assert.Single(result);
        Assert.Equal("complete-level-6", user.Id);
    }

    [Fact]
    public void ConsolidateForSynchronization_WhenNoAccountHasHdrGroup_IgnoresAccountsOutsideHdr()
    {
        var users = new[]
        {
            CreateUser("old-account", "JDIAZ@bdoargentina.com.ar", null),
            CreateUser("new-account", "JDIAZ@bdoargentina.com.ar", null)
        };

        var result = DirectoryUserSyncConsolidator.ConsolidateForSynchronization(users);

        Assert.Empty(result);
    }

    private static DirectoryUserSyncRecord CreateUser(string id, string mail, GroupConfig? group) => new()
    {
        Id = id,
        Mail = mail,
        GivenName = "Nombre",
        Surname = "Apellido",
        Department = "Area",
        HighestGroup = group
    };
}
