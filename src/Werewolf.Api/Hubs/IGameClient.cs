using Werewolf.Api.Rooms;

namespace Werewolf.Api.Hubs;

public interface IGameClient
{
    Task LobbyUpdated(LobbyDto lobby);
    Task RoleAssigned(RoleDto role);
    Task PhaseChanged(PhaseDto phase);
    Task NightResolved(string? killedName);
    Task SeerResult(string targetName, bool isWolf);
    Task VoteTally(IReadOnlyDictionary<Guid, int> tally);
    Task DayResolved(string? eliminatedName);
    Task WolfChat(string from, string message);
    Task GameOver(GameOverDto result);
    Task TownChat(string from, string message);
}