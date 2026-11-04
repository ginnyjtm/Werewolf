using Werewolf.Engine;

namespace Werewolf.Api.Rooms;

public sealed class GameRoom(string code)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Code { get; } = code;
    public Game Game { get; } = new();
    public Guid HostId { get; set; }
    public DateTimeOffset? PhaseEndsAt { get; set; }

    /// <summary>playerId → connectionId (bots have none)</summary>
    public Dictionary<Guid, string> Connections { get; } = new();

    public string Group => $"room-{Code}";
    public string WolfGroup => $"room-{Code}-wolves";

    /// <summary>Game isn't thread-safe; every mutation goes through here.</summary>
    public async Task RunAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try { await action(); }
        finally { _gate.Release(); }
    }
}