using UnityEngine;

public struct GameEndEvent : IEvent
{
    public bool IsWhiteWinner { get; }
    public bool IsBlackWinner { get; }
    public bool IsDraw { get; }
    public bool IsStalemated { get; }
    public GameEndEvent(bool isWhiteWinner, bool isBlackWinner, bool isDraw, bool isStalemated = false)
    {
        IsWhiteWinner = isWhiteWinner;
        IsBlackWinner = isBlackWinner;
        IsDraw = isDraw;
        IsStalemated = isStalemated;
    }
}
