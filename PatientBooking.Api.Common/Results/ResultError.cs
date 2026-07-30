namespace PatientBooking.Api.Common.Results;

public readonly record struct ResultError(string Code, string Description)
{
    // Shared "no error". Callers can compare against it instead of
    // allocating a new one every time
    public static readonly ResultError None = new("", "");

    // True when this carries no meaningful code
    public bool IsNone => string.IsNullOrWhiteSpace(Code);
}
