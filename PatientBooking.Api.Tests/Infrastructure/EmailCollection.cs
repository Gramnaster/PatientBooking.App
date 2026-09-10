using Xunit;

namespace PatientBooking.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class EmailCollection : ICollectionFixture<EmailTestFixture>
{
    public const string Name = "Email";
}
