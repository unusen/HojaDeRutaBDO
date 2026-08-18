namespace HojaDeRuta.Services;

internal static class DirectoryUserSyncFormatter
{
    internal static string? NormalizeMail(string? mail)
    {
        if (string.IsNullOrWhiteSpace(mail))
        {
            return null;
        }

        var localPart = mail.Trim().Split('@', 2, StringSplitOptions.TrimEntries)[0];
        return string.IsNullOrWhiteSpace(localPart) ? null : localPart.ToUpperInvariant();
    }

    internal static string BuildDetail(string surname, string givenName) =>
        $"{surname.Trim().ToUpperInvariant()}, {givenName.Trim().ToUpperInvariant()}";
}
