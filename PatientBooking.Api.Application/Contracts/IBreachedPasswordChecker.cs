namespace PatientBooking.Api.Application.Contracts;

/// <summary>
/// Boundary check used by RegisterUserDtoValidator, ResetPasswordDtoValidator.
/// Separate from Identity's Password complexity rules, which only checks for
/// password SHAPE (length, character classes), not whether the password
/// is already public knowledge.
/// </summary>
public interface IBreachedPasswordChecker
{
    Task<bool> IsBreachedAsync(string password, CancellationToken ct);
}
