namespace CompTracker.Services;

// Singleton signal raised after any admin save so open dashboards (on any device) re-render.
public class ChangeNotifier
{
    public event Action? Changed;

    public void Notify() => Changed?.Invoke();
}
