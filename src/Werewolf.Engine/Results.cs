namespace Werewolf.Engine;

public sealed record NightResult(Player? Killed, Player? SeerTarget, bool SeerSawWolf);
public sealed record VoteResult(Player? Eliminated, IReadOnlyDictionary<Guid, int> Tally);