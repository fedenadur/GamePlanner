using System.Collections.Concurrent;
using GamePlanner.API.Domain;

namespace GamePlanner.API.Services;

public class SteamTokenStore
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (AuthResponse Response, DateTime Expiry)> _store = new();

    public int Count => _store.Count;

    public string Store(AuthResponse response, TimeSpan? lifetime = null)
    {
        var code = Guid.NewGuid().ToString("N");
        _store[code] = (response, DateTime.UtcNow.Add(lifetime ?? DefaultLifetime));
        return code;
    }

    public AuthResponse? Redeem(string code)
    {
        if (!_store.TryRemove(code, out var entry)) return null;
        if (entry.Expiry < DateTime.UtcNow) return null;
        return entry.Response;
    }

    /// <summary>Removes every entry whose Expiry has already passed. Returns the number removed.</summary>
    public int RemoveExpired()
    {
        var now = DateTime.UtcNow;
        var removed = 0;

        foreach (var kvp in _store)
        {
            if (kvp.Value.Expiry < now && _store.TryRemove(kvp.Key, out _))
                removed++;
        }

        return removed;
    }
}
