using UnityEngine;
using ChessDotNet;
using System.Collections.Generic;

/// <summary>
/// GameManager is a singleton class that manages the chess game state.
/// It handles starting a new game, making moves, and keeping track of the current player and move history.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    /// <summary>
    /// Stores the current chess game instance.
    /// </summary>
    private ChessGame _game;

    /// <summary>
    /// Stores the current player whose turn it is to play.
    /// </summary>
    private ChessDotNet.Player _currentPlayer;

    /// <summary>
    /// Stores the history of moves made in the game.
    /// This is used to keep track of the moves for Stockfish analysis and game state management.
    /// </summary>
    private List<string> _moveHistory = new List<string>();

    /// <summary>
    /// Starts a new chess game.
    /// </summary>
    public void StartGame()
    {
        // Check if a game is already in progress => clear board
        if (_game != null)
        {
            // Ping every listeners that the board should be cleared
            EventBus<ClearBoardEvent>.Raise(new ClearBoardEvent());
            Debug.Log("Clearing previous game board...");
        }

        // Initialize a new chess game
        _game = new ChessGame();

        // Clear previous move history
        _moveHistory.Clear();

        // Ping every listeners that a new game has started
        EventBus<NewGameEvent>.Raise(new NewGameEvent
        {
            Fen = _game.GetFen(),
            IsWhiteSide = true // Variable is not used, but can be useful for UI logic
        });


        Debug.Log("Game started!");

        // Set the starting player
        _currentPlayer = ChessDotNet.Player.White;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pgn"></param>
    public void StartGame(string pgn)
    {
        // Check if a game is already in progress => clear board
        if (_game != null)
        {
            // Ping every listeners that the board should be cleared
            EventBus<ClearBoardEvent>.Raise(new ClearBoardEvent());
            Debug.Log("Clearing previous game board...");
        }

        // Initialize a new chess game from PGN
        PgnReader<ChessGame> pgnReader = new PgnReader<ChessGame>();
        pgnReader.ReadPgnFromString(pgn);
        _game = pgnReader.Game;

        // Clear previous move history
        _moveHistory.Clear();

        // Ping every listeners that a new game has started
        EventBus<NewGameEvent>.Raise(new NewGameEvent
        {
            Fen = _game.GetFen(),
            IsWhiteSide = true // Variable is not used, but can be useful for UI logic
        });

        Debug.Log("Daily puzzle game started!");

        // Set the starting player based on the PGN
        _currentPlayer = _game.WhoseTurn;
    }


    /// <summary>
    /// Makes a move in the chess game.
    /// This method validates the move, updates the game state, and notifies Stockfish of the move history.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="promotionPiece"></param>
    /// <returns></returns>
    public MoveType MakeMove(string from, string to, char? promotionPiece = null)
    {
        // Validate input
        if (_game == null)
        {
            Debug.LogError("No game in progress. Please start a new game first.");
            return MoveType.Invalid;
        }

        // Create a move object
        Move move = new Move(from, to, _currentPlayer, promotionPiece);

        // Check if the move is valid
        bool isValid = _game.IsValidMove(move);

        if (isValid)
        {
            // Example move string format: "e2e4" or "e7e8q" for promotion
            string moveString = $"{from}{to}{(promotionPiece.HasValue ? promotionPiece.Value.ToString() : string.Empty)}";

            // Store the move in history
            _moveHistory.Add(moveString.ToLower());

            // Notify Stockfish of the move history
            StockfishController.Instance.SetPositionWithMoves(_moveHistory);

            // Request Stockfish to find the best move for the next turn
            StockfishController.Instance.FindBestMove();

            // Switch the current player
            _currentPlayer = _currentPlayer == ChessDotNet.Player.White ? ChessDotNet.Player.Black : ChessDotNet.Player.White;

            // Make the move in the game
            MoveType moveType = _game.MakeMove(move, true);

            Debug.Log($"Move made: {from} to {to}, MoveType: {moveType}");

            //Check if the game is over
            if (_game.IsCheckmated(_currentPlayer))
            {
                Debug.LogWarning($"Game over! {_currentPlayer} is checkmated.");
            }

            return moveType;
        }

        return MoveType.Invalid;
    }

    /// <summary>
    /// Reloads the current game state.
    /// </summary>
    public void ReloadGame()
    {
        if (_game != null)
        {
            // Notify all listeners that the board should be reloaded with the current game state
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
