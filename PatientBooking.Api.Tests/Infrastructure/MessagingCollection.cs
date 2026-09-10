using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class MessagingCollection : ICollectionFixture<MessagingTestFixture>
{
    public const string Name = "Messaging";
}
