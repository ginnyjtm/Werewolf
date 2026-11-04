namespace Werewolf.Api.Rooms;

public sealed class PhaseTicker(RoomManager rooms, GameService games, ILogger<PhaseTicker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var room in rooms.All)
            {
                if (room.PhaseEndsAt is not { } endsAt || endsAt > DateTimeOffset.UtcNow) continue;

                try
                {
                    await room.RunAsync(() =>
                        room.PhaseEndsAt <= DateTimeOffset.UtcNow   // re-check inside the lock
                            ? games.AdvanceAsync(room)
                            : Task.CompletedTask);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to advance room {Code}", room.Code);
                }
            }
        }
    }
}