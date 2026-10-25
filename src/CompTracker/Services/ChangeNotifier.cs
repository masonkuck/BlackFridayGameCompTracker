namespace CompTracker.Services;

// Singleton signal raised after any admin save so open dashboards (on any device) re-render.
public class ChangeNotifier
{
    public event Action? Changed;

    // Extra signal for a saved score, carrying any new-leader celebrations (dashboard sounds and animation).
    public event Action<ScorePosted>? ScorePosted;

    public void Notify() => Changed?.Invoke();

    public void Notify(ScorePosted posted)
    {
        Changed?.Invoke();
        ScorePosted?.Invoke(posted);
    }
}
