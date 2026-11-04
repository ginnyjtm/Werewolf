using Werewolf.Engine;

namespace Werewolf.Engine.Tests;

public class GameTests
{
    // wolf, seer, doctor, villager, villager — in join order
    private static (Game game, Player[] p) NewGame()
    {
        var game = new Game();
        var p = new[] { "Wolf", "Seer", "Doc", "Vil1", "Vil2" }
            .Select(n => game.AddPlayer(n)).ToArray();
        game.Start([Role.Werewolf, Role.Seer, Role.Doctor, Role.Villager, Role.Villager]);
        return (game, p);
    }

    [Fact]
    public void Doctor_save_prevents_the_kill()
    {
        var (game, p) = NewGame();
        game.SubmitNightAction(p[0].Id, p[3].Id); // wolf → Vil1
        game.SubmitNightAction(p[2].Id, p[3].Id); // doc saves Vil1

        var result = game.ResolveNight();

        Assert.Null(result.Killed);
        Assert.True(p[3].IsAlive);
        Assert.Equal(Phase.Day, game.Phase);
    }

    [Fact]
    public void Seer_detects_the_wolf()
    {
        var (game, p) = NewGame();
        game.SubmitNightAction(p[1].Id, p[0].Id);

        var result = game.ResolveNight();

        Assert.True(result.SeerSawWolf);
    }

    [Fact]
    public void Tied_vote_eliminates_nobody()
    {
        var (game, p) = NewGame();
        game.ResolveNight();
        game.StartVoting();
        game.CastVote(p[1].Id, p[3].Id);
        game.CastVote(p[2].Id, p[4].Id);

        var result = game.ResolveVote();

        Assert.Null(result.Eliminated);
        Assert.Equal(Phase.Night, game.Phase);
    }

    [Fact]
    public void Village_wins_when_wolf_is_voted_out()
    {
        var (game, p) = NewGame();
        game.ResolveNight();
        game.StartVoting();
        foreach (var voter in p.Skip(1))
            game.CastVote(voter.Id, p[0].Id);

        game.ResolveVote();

        Assert.Equal(Team.Village, game.Winner);
        Assert.Equal(Phase.GameOver, game.Phase);
    }
}