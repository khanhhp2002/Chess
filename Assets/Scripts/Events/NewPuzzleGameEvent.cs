using UnityEngine;

public struct NewPuzzleGameEvent : IEvent
{
    public string blackPlayerName { get; }
    public string whitePlayerName { get; }
    public string blackPlayerElo { get; }
    public string whitePlayerElo { get; }
    public string[] solution { get; }
    public string[] themes;
    public NewPuzzleGameEvent(string blackPlayerName, string whitePlayerName, string blackPlayerElo, string whitePlayerElo, string[] solution, string[] themes)
    {
        this.blackPlayerName = blackPlayerName;
        this.whitePlayerName = whitePlayerName;
        this.blackPlayerElo = blackPlayerElo;
        this.whitePlayerElo = whitePlayerElo;
        this.solution = solution;
        this.themes = themes;
    }
}
