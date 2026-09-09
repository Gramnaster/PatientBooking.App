using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Common.Models.Config;

public sealed class AdminSeedSettings
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
