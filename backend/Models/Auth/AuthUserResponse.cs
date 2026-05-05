namespace backend.Models.Auth;

public sealed class AuthUserResponse
{
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}
