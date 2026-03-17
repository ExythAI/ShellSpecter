namespace ShellSpecter.Seer.Services;

/// <summary>
/// Simple auth state manager for the Seer dashboard.
/// </summary>
public sealed class AuthService
{
    public string? Token { get; private set; }
    public string? Endpoint { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnAuthStateChanged;

    public void SetToken(string token, string endpoint)
    {
        Token = token;
        Endpoint = endpoint;
        OnAuthStateChanged?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        OnAuthStateChanged?.Invoke();
    }
}
