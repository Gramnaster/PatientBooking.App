using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace PatientBooking.Api.Domain;

/// <summary>
/// Persistent refresh token, backing UsersService.RefreshTokenAsync/RevokeRefreshTokenAsync.
/// Only a SHA-256 hash of the token is stored - same as not storing plaintext passwords.
/// </summary>
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(256)]
    public required string TokenHash { get; set; }

    // Should probably be non-nullable because every token needs an expiry
    public DateTimeOffset ExpiresAtUtc { get; set; }
    // Not nullable becauase every token has a creation moment
    public DateTimeOffset CreatedAtUtc { get; set; }

    // Null = still active. Set the moment the token is used (rotation) or explicity logged out
    public DateTimeOffset? RevokedAtUtc { get; set; }

    // Points at the row that replaced this one on rotation - audit trail for spotting reuse
    [MaxLength(256)]
    public string? ReplacedByTokenHash { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    [NotMapped]
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;
}
