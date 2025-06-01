using UnityEngine;
using ChessDotNet;
using System.Collections.Generic;

public class GameManager : Singleton<GameManager>
{
    private ChessGame _game;
    private Player _currentPlayer;

    private List<string> _moveHistory = new List<string>();

    [ContextMenu("Start Game")]
    public void StartGame()
    {
        // Check if a game is already in progress => clear board
        if (_game != null)
        {
            EventBus<ClearBoardEvent>.Raise(new ClearBoardEvent());
            Debug.Log("Clearing previous game board...");
        }

        // Initialize a new chess game
        _game = new ChessGame();
        _moveHistory.Clear(); // Clear previous move history
        EventBus<NewGameEvent>.Raise(new NewGameEvent
        {
            Fen = _game.GetFen(),
            IsWhiteSide = true // Assuming the game starts with white side
        });
        Debug.Log("Game started!");

        _currentPlayer = Player.White; // Set the starting player
    }

    public MoveType MakeMove(string from, string to,char? promotionPiece = null)
    {
        if (_game == null)
        {
            Debug.LogError("No game in progress. Please start a new game first.");
            return MoveType.Invalid;
        }

        Move move = new Move(from, to, _currentPlayer,promotionPiece);
        bool isValid = _game.IsValidMove(move);

        if (isValid)
        {
            // If the move is valid, switch the current player
            string moveString = $"{from}{to}{(promotionPiece.HasValue ? promotionPiece.Value.ToString() : string.Empty)}";
            _moveHistory.Add(moveString.ToLower()); // Store the move in history
            StockfishController.Instance.SetPositionWithMoves(_moveHistory); // Notify Stockfish of the move history
            StockfishController.Instance.FindBestMove(); // Request Stockfish to find the best move

            _currentPlayer = _currentPlayer == Player.White ? Player.Black : Player.White;
            MoveType moveType = _game.MakeMove(move, true);
            Debug.Log($"Move made: {from} to {to}, MoveType: {moveType}");
            return moveType;
        }

        return MoveType.Invalid;
    }

    public void ReloadGame()
    {
        if (_game != null)
        {
            EventBus<ReloadBoardEvent>.Raise(new ReloadBoardEvent
            {
                Fen = _game.GetFen()
            });
        }
        else
        {
            Debug.LogWarning("No game in progress to reload.");
        }
    }
}
