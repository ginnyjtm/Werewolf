using System.Collections.Concurrent;

namespace Werewolf.Api.Rooms;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, (string Code, Guid PlayerId)> _bindings = new();

    public IEnumerable<GameRoom> All => _rooms.Values;

    public GameRoom Create()
    {
        while (true)
        {
            var code = Random.Shared.Next(1000, 10000).ToString();
            var room = new GameRoom(code);
            if (_rooms.TryAdd(code, room)) return room;
        }
    }

    public GameRoom? Get(string code) => _rooms.GetValueOrDefault(code);

    // The server decides who you are from your connection, so clients can't pretend to be someone else.
    public void Bind(string connectionId, string code, Guid playerId) => _bindings[connectionId] = (code, playerId);
    public bool TryGetBinding(string connectionId, out (string Code, Guid PlayerId) b) => _bindings.TryGetValue(connectionId, out b);
    public void Unbind(string connectionId) => _bindings.TryRemove(connectionId, out _);
}