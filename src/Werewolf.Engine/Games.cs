namespace Werewolf.Engine;

public sealed class Game
{
    public const int MinPlayers = 5;

    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, Guid> _wolfVotes = new();
    private readonly Dictionary<Guid, Guid> _dayVotes = new();
    private Guid? _seerTarget;
    private Guid? _doctorTarget;

    public Phase Phase { get; private set; } = Phase.Lobby;
    public int Round { get; private set; }
    public Team? Winner { get; private set; }
    public IReadOnlyList<Player> Players => _players;
    public IEnumerable<Player> Alive => _players.Where(p => p.IsAlive);

    public IReadOnlyDictionary<Guid, int> CurrentTally =>
    _dayVotes.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());

    // ---------- Lobby ----------

    public Player AddPlayer(string name, bool isBot = false)
    {
        EnsurePhase(Phase.Lobby);
        var player = new Player(name, isBot);
        _players.Add(player);
        return player;
    }

    /// <summary>Random role assignment.</summary>
    public void Start(Random? rng = null)
    {
        rng ??= Random.Shared;
        var roles = BuildRoles(_players.Count).OrderBy(_ => rng.Next()).ToList();
        Start(roles);
    }

    /// <summary>Assigns roles in join order. Handy for tests.</summary>
    public void Start(IReadOnlyList<Role> roles)
    {
        EnsurePhase(Phase.Lobby);
        if (_players.Count < MinPlayers)
            throw new InvalidOperationException($"Need at least {MinPlayers} players.");
        if (roles.Count != _players.Count)
            throw new ArgumentException("Role count must match player count.");

        for (var i = 0; i < _players.Count; i++)
            _players[i].Role = roles[i];

        Round = 1;
        Phase = Phase.Night;
    }

    private static List<Role> BuildRoles(int count)
    {
        var wolves = count >= 8 ? 2 : 1;
        var roles = new List<Role>();
        roles.AddRange(Enumerable.Repeat(Role.Werewolf, wolves));
        roles.Add(Role.Seer);
        roles.Add(Role.Doctor);
        roles.AddRange(Enumerable.Repeat(Role.Villager, count - roles.Count));
        return roles;
    }

    // ---------- Night ----------

    public void SubmitNightAction(Guid actorId, Guid targetId)
    {
        EnsurePhase(Phase.Night);
        var actor = GetAlive(actorId);
        var target = GetAlive(targetId);

        switch (actor.Role)
        {
            case Role.Werewolf:
                if (target.Role == Role.Werewolf)
                    throw new InvalidOperationException("Wolves can't target wolves.");
                _wolfVotes[actor.Id] = target.Id;
                break;
            case Role.Seer:
                _seerTarget = target.Id;
                break;
            case Role.Doctor:
                _doctorTarget = target.Id;
                break;
            default:
                throw new InvalidOperationException("Villagers have no night action.");
        }
    }

    public NightResult ResolveNight()
    {
        EnsurePhase(Phase.Night);

        Player? killed = null;
        var victimId = MostVoted(_wolfVotes.Values);
        if (victimId is { } id && id != _doctorTarget)   // doctor save wins
        {
            killed = Find(id);
            killed.IsAlive = false;
        }

        var seen = _seerTarget is { } s ? Find(s) : null;
        var result = new NightResult(killed, seen, seen?.Role == Role.Werewolf);

        _wolfVotes.Clear();
        _seerTarget = null;
        _doctorTarget = null;
        Phase = CheckWinner() ? Phase.GameOver : Phase.Day;
        return result;
    }

    // ---------- Day ----------

    public void StartVoting()
    {
        EnsurePhase(Phase.Day);
        Phase = Phase.Voting;
    }

    public void CastVote(Guid voterId, Guid targetId)
    {
        EnsurePhase(Phase.Voting);
        GetAlive(voterId);
        GetAlive(targetId);
        _dayVotes[voterId] = targetId;   // re-voting overwrites
    }

    public VoteResult ResolveVote()
    {
        EnsurePhase(Phase.Voting);

        var tally = _dayVotes.Values
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());

        Player? eliminated = null;
        if (MostVoted(_dayVotes.Values) is { } id)
        {
            eliminated = Find(id);
            eliminated.IsAlive = false;
        }

        _dayVotes.Clear();
        if (CheckWinner())
        {
            Phase = Phase.GameOver;
        }
        else
        {
            Round++;
            Phase = Phase.Night;
        }
        return new VoteResult(eliminated, tally);
    }

    // ---------- Helpers ----------

    /// <summary>Returns the top target, or null if no votes or a tie.</summary>
    private static Guid? MostVoted(IEnumerable<Guid> votes)
    {
        var ranked = votes.GroupBy(v => v)
            .Select(g => (Target: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        if (ranked.Count == 0) return null;
        if (ranked.Count > 1 && ranked[0].Count == ranked[1].Count) return null;
        return ranked[0].Target;
    }

    private bool CheckWinner()
    {
        var wolves = Alive.Count(p => p.Role == Role.Werewolf);
        var others = Alive.Count() - wolves;

        if (wolves == 0) Winner = Team.Village;
        else if (wolves >= others) Winner = Team.Wolves;

        return Winner is not null;
    }

    private Player Find(Guid id) =>
        _players.FirstOrDefault(p => p.Id == id)
        ?? throw new InvalidOperationException("Unknown player.");

    private Player GetAlive(Guid id)
    {
        var p = Find(id);
        if (!p.IsAlive) throw new InvalidOperationException($"{p.Name} is dead.");
        return p;
    }

    private void EnsurePhase(Phase expected)
    {
        if (Phase != expected)
            throw new InvalidOperationException($"Expected {expected}, but game is in {Phase}.");
    }
}