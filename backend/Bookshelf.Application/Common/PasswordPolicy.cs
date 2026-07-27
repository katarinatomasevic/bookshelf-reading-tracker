using System.Text.RegularExpressions;
using Bookshelf.Application.Common.Exceptions;

namespace Bookshelf.Application.Common;

/// <summary>
/// Shared password strength rule for registration and password changes — kept in one place
/// so the two call sites (and the frontend hint text) can never drift out of sync.
/// </summary>
public static partial class PasswordPolicy
{
    public const string RequirementsMessage =
        "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number, and a special character.";

    public static void Validate(string password)
    {
        if (!Regex().IsMatch(password))
        {
            throw new ValidationException(RequirementsMessage);
        }
    }

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$")]
    private static partial Regex Regex();
}
