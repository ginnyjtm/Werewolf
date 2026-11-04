using Microsoft.AspNetCore.SignalR;
using Werewolf.Api.Rooms;
using Werewolf.Engine;

namespace Werewolf.Api.Hubs;

public sealed class GameHub(RoomManager rooms, GameService games) : Hub<IGameClient>
{
    private static readonly string[] BotNames =
        ["Bot Barry", "Bot Luna", "Bot Hank", "Bot Mira", "Bot Otis", "Bot Pip", "Bot Quinn", "Bot Rex"];

    public Task<JoinResult> CreateRoom(string name) => Join(rooms.Create(), name, isHost: true);

    public Task<JoinResult> JoinRoom(string code, string name) =>
        Join(rooms.Get(code) ?? throw new HubException("Room not found."), name, isHost: false);

    public Task AddBots(int count) => AsHost(async room =>
    {
        var used = room.Game.Players.Select(p => p.Name).ToHashSet();
        foreach (var name in BotNames.Where(n => !used.Contains(n)).Take(count))
            room.Game.AddPlayer(name, isBot: true);
        await games.BroadcastLobbyAsync(room);
    });

    public Task StartGame() => AsHost(games.StartGameAsync);

    public Task NightAction(Guid targetId) => InMyRoom((room, me) =>
    {
        room.Game.SubmitNightAction(me, targetId);
        return Task.CompletedTask;
    });

    public Task Vote(Guid targetId) => InMyRoom(async (room, me) =>
    {
        room.Game.CastVote(me, targetId);
        await games.BroadcastTallyAsync(room);
    });

    public Task SendWolfChat(string message) => InMyRoom(async (room, me) =>
    {
        var player = room.Game.Players.First(p => p.Id == me);
        if (player.Role != Role.Werewolf || !player.IsAlive)
            throw new HubException("Only living wolves can use the wolf chat.");
        await Clients.Group(room.WolfGroup).WolfChat(player.Name, message);
    });

    public Task SendTownChat(string message) => InMyRoom(async (room, me) =>
    {
        var player = room.Game.Players.First(p => p.Id == me);
        if (!player.IsAlive)
            throw new HubException("The dead can't speak. 👻");
        if (room.Game.Phase is not (Phase.Day or Phase.Voting))
            throw new HubException("You can only talk during the day.");

        await Clients.Group(room.Group).TownChat(player.Name, message);
    });

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        rooms.Unbind(Context.ConnectionId);   // reconnection: a future session
        return base.OnDisconnectedAsync(exception);
    }

    // ---------- Helpers ----------

    private async Task<JoinResult> Join(GameRoom room, string name, bool isHost)
    {
        JoinResult? result = null;
        await Guarded(room, async () =>
        {
            var player = room.Game.AddPlayer(name);
            if (isHost) room.HostId = player.Id;

            room.Connections[player.Id] = Context.ConnectionId;
            rooms.Bind(Context.ConnectionId, room.Code, player.Id);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.Group);
            await games.BroadcastLobbyAsync(room);

            result = new JoinResult(room.Code, player.Id);
        });
        return result!;
    }

    private Task InMyRoom(Func<GameRoom, Guid, Task> action)
    {
        if (!rooms.TryGetBinding(Context.ConnectionId, out var b))
            throw new HubException("You're not in a room.");
        var room = rooms.Get(b.Code) ?? throw new HubException("Room no longer exists.");
        return Guarded(room, () => action(room, b.PlayerId));
    }

    private Task AsHost(Func<GameRoom, Task> action) => InMyRoom((room, me) =>
        me == room.HostId ? action(room) : throw new HubException("Only the host can do that."));

    /// <summary>Lock the room and turn engine rule violations into messages the client can show.</summary>
    private static Task Guarded(GameRoom room, Func<Task> action) => room.RunAsync(async () =>
    {
        try { await action(); }
        catch (InvalidOperationException ex) { throw new HubException(ex.Message); }
    });

    
}