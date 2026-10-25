namespace CompTracker.Services;

// Per-browser-connection (scoped = one Blazor circuit) admin unlock state.
// Unlocked by entering the PIN on any admin page; locked again whenever the dashboard is opened
// or the page is reloaded (a reload starts a new circuit).
public class AdminSession(IConfiguration config)
{
    public bool IsUnlocked { get; private set; }

    public bool TryUnlock(string? pin)
    {
        var expected = config["AdminPin"];
        IsUnlocked = !string.IsNullOrEmpty(expected) && pin?.Trim() == expected;
        return IsUnlocked;
    }

    public void Lock() => IsUnlocked = false;
}
