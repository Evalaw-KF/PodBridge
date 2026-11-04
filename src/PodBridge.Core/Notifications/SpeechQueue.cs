namespace PodBridge.Core.Notifications;

/// <summary>A short first-in-first-out queue of things to say. When it overflows the oldest are dropped.</summary>
public sealed class SpeechQueue
{
    private readonly Queue<string> _items = new();
    private readonly int _max;

    public SpeechQueue(int max = 5) => _max = Math.Max(1, max);

    public int Count => _items.Count;

    /// <summary>Adds text. Returns the number of older items dropped to make room.</summary>
    public int Enqueue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        int dropped = 0;
        while (_items.Count >= _max)
        {
            _items.Dequeue();
            dropped++;
        }
        _items.Enqueue(text);
        return dropped;
    }

    public bool TryDequeue(out string text) => _items.TryDequeue(out text!);

    public void Clear() => _items.Clear();
}
