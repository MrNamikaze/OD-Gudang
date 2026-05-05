namespace backend.Configuration;

public sealed class SeedUserOptions
{
    public const string SectionName = "SeedUsers";

    public SeedAccount Admin { get; set; } = new();
    public SeedAccount User { get; set; } = new();
}

public sealed class SeedAccount
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
