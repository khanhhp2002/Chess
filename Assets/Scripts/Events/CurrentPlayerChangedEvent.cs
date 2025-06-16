public struct CurrentPlayerChangedEvent : IEvent
{
    public ChessDotNet.Player CurrentPlayer { get; }
    public CurrentPlayerChangedEvent(ChessDotNet.Player currentPlayer)
    {
        CurrentPlayer = currentPlayer;
    }
}
