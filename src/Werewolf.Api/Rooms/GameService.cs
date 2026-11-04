using Microsoft.AspNetCore.SignalR;
using Werewolf.Api.Hubs;
using Werewolf.Engine;

namespace Werewolf.Api.Rooms;

public sealed class GameService(IHubContext<GameHub, IGameClient> hub)
{
    private static readonly TimeSpan NightLength = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DayLength = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan VoteLength = TimeSpan.FromSeconds(15);

    public Task BroadcastLobbyAsync(GameRoom room) =>
        hub.Clients.Group(room.Group).LobbyUpdated(
            new LobbyDto(room.Code, room.HostId, room.Game.Players.Select(p => p.ToDto()).ToList()));

    public Task BroadcastTallyAsync(GameRoom room) =>
        hub.Clients.Group(room.Group).VoteTally(room.Game.CurrentTally);

    public async Task StartGameAsync(GameRoom room)
    {
        room.Game.Start();
        var wolves = room.Game.Players.Where(p => p.Role == Role.Werewolf).ToList();

        // Each human gets ONLY their own role, privately.
        foreach (var p in room.Game.Players)
        {
            if (!room.Connections.TryGetValue(p.Id, out var conn)) continue; // bot

            var fellows = new List<PlayerDto>();
            if (p.Role == Role.Werewolf)
            {
                await hub.Groups.AddToGroupAsync(conn, room.WolfGroup);
                fellows = wolves.Where(w => w.Id != p.Id).Select(w => w.ToDto()).ToList();
            }
            await hub.Clients.Client(conn).RoleAssigned(new RoleDto(p.Role, fellows));
        }

        await EnterPhaseAsync(room);
    }

    /// <summary>Called by the ticker when the phase timer runs out.</summary>
    public async Task AdvanceAsync(GameRoom room)
    {
        var game = room.Game;
        var everyone = hub.Clients.Group(room.Group);

        switch (game.Phase)
        {
            case Phase.Night:
                var night = game.ResolveNight();
                await everyone.NightResolved(night.Killed?.Name);

                if (night.SeerTarget is { } target)
                {
                    var seer = game.Players.First(p => p.Role == Role.Seer);
                    if (room.Connections.TryGetValue(seer.Id, out var conn))
                        await hub.Clients.Client(conn).SeerResult(target.Name, night.SeerSawWolf);
                }
                break;

            case Phase.Day:
                game.StartVoting();
                break;

            case Phase.Voting:
                var vote = game.ResolveVote();
                await everyone.DayResolved(vote.Eliminated?.Name);
                break;
        }

        await EnterPhaseAsync(room);
    }

    private async Task EnterPhaseAsync(GameRoom room)
    {
        var game = room.Game;

        if (game.Phase == Phase.GameOver)
        {
            room.PhaseEndsAt = null;
            var reveal = game.Players.Select(p => new RevealDto(p.Id, p.Name, p.Role, p.IsAlive)).ToList();
            await hub.Clients.Group(room.Group).GameOver(new GameOverDto(game.Winner!.Value, reveal));
            return;
        }

        var length = game.Phase switch
        {
            Phase.Night => NightLength,
            Phase.Day => DayLength,
            Phase.Voting => VoteLength,
            _ => throw new InvalidOperationException($"No timer for {game.Phase}")
        };
        room.PhaseEndsAt = DateTimeOffset.UtcNow + length;

        await hub.Clients.Group(room.Group).PhaseChanged(new PhaseDto(
            game.Phase, game.Round, room.PhaseEndsAt.Value,
            game.Players.Select(p => p.ToDto()).ToList()));

        if (game.Phase == Phase.Night) BotNightActions(game);
        if (game.Phase == Phase.Voting) await BotVotesAsync(room);
    }

    // ---------- Bots: dumb but enough for a solo demo ----------

    private static void BotNightActions(Game game)
    {
        foreach (var bot in game.Alive.Where(p => p.IsBot && p.Role != Role.Villager).ToList())
        {
            var targets = game.Alive
                .Where(t => t.Id != bot.Id)
                .Where(t => !(bot.Role == Role.Werewolf && t.Role == Role.Werewolf))
                .ToList();
            if (targets.Count > 0)
                game.SubmitNightAction(bot.Id, targets[Random.Shared.Next(targets.Count)].Id);
        }
    }

    private async Task BotVotesAsync(GameRoom room)
    {
        foreach (var bot in room.Game.Alive.Where(p => p.IsBot).ToList())
        {
            var targets = room.Game.Alive.Where(t => t.Id != bot.Id).ToList();
            room.Game.CastVote(bot.Id, targets[Random.Shared.Next(targets.Count)].Id);
        }
        await BroadcastTallyAsync(room);
    }
}