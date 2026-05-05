using System.Collections.Concurrent;
using GamePlanner.API.Domain;

namespace GamePlanner.API.Services;

public class SteamTokenStore
{
    private readonly ConcurrentDictionary<string, (AuthResponse Response, DateTime Expiry)> _store = new();

    public string Store(AuthResponse response)
    {
        var code = Guid.NewGuid().ToString("N");
        _store[code] = (response, DateTime.UtcNow.AddMinutes(5));
        return code;
    }

    public AuthResponse? Redeem(string code)
    {
        if (!_store.TryRemove(code, out var entry)) return null;
        if (entry.Expiry < DateTime.UtcNow) return null;
        return entry.Response;
    }
}
