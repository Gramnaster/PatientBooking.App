using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace PatientBooking.Api.Domain;

public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(256)]
    public required string TokenHash { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }

    // Null = still active. Set the moment the token is used (rotation) or explicity logged out
    public DateTimeOffset? RevokedAtUtc { get; set; }

    // Points at the row that replaced this one on rotation - audit trail for spotting reuse
    [MaxLength(256)]
    public string? ReplacedByTokenHash { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAtUtc is null & ExpiresAtUtc > DateTimeOffset.UtcNow;
}
