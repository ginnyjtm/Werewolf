namespace Werewolf.Engine;

public sealed class Player
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public bool IsBot { get; }
    public Role Role { get; internal set; }
    public bool IsAlive { get; internal set; } = true;

    public Player(string name, bool isBot = false)
    {
        Name = name;
        IsBot = isBot;
    }
}