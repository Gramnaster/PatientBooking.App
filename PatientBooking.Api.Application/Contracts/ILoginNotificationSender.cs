using PatientBooking.Api.Domain;

namespace PatientBooking.Api.Application.Contracts;

public interface ILoginNotificationSender
{
    Task SendLoginNotificationAsync(ApplicationUser user, string ipAddress, DateTimeOffset occuredAtUtc, CancellationToken ct);
}
