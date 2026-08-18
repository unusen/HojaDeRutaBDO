using HojaDeRuta.Models.Config;

namespace HojaDeRuta.Models.DTO;

public sealed class DirectoryUserSyncRecord
{
    public string? Id { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? Mail { get; init; }
    public string? Department { get; init; }
    public GroupConfig? HighestGroup { get; init; }
}
