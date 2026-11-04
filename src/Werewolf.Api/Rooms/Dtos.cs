using Werewolf.Engine;

namespace Werewolf.Api.Rooms;

public record PlayerDto(Guid Id, string Name, bool IsAlive, bool IsBot);
public record LobbyDto(string RoomCode, Guid HostId, IReadOnlyList<PlayerDto> Players);
public record PhaseDto(Phase Phase, int Round, DateTimeOffset EndsAt, IReadOnlyList<PlayerDto> Players);
public record RoleDto(Role Role, IReadOnlyList<PlayerDto> FellowWolves);
public record RevealDto(Guid Id, string Name, Role Role, bool IsAlive);
public record GameOverDto(Team Winner, IReadOnlyList<RevealDto> Players);
public record JoinResult(string RoomCode, Guid PlayerId);

public static class DtoMapping
{
    public static PlayerDto ToDto(this Player p) => new(p.Id, p.Name, p.IsAlive, p.IsBot);
}