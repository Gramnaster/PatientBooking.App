using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace PatientBooking.Api.Domain;

public class PatientBookingDbContext(DbContextOptions<PatientBookingDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    //public DbSet<>
}
