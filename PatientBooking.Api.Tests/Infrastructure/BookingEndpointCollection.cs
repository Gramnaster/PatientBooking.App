using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class BookingEndpointCollection : ICollectionFixture<BookingEndpointTestFixture>
{
    public const string Name = "BookingEndpoint";
}
