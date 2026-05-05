using System.ComponentModel.DataAnnotations;

namespace backend.Models.Auth;

public sealed class LoginRequest
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Password { get; set; } = string.Empty;
}
