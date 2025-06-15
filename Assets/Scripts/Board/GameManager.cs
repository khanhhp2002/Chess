using UnityEngine;
using ChessDotNet;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    /// Stores the side of the AI player.
    /// This is used to determine which side the AI will play as when making moves.
    /// </summary>
    private ChessDotNet.Player _aiSide = ChessDotNet.Player.Black;

    /// <summary>
    /// Indicates whether the AI is allowed to make a move.
    /// This can be used to control when the AI is allowed to play, such as during a puzzle or when the player is ready.
    /// </summary>
    private bool _isStockFishAllowed = true;

    /// <summary>
    /// Indicates whether the game is in puzzle mode.
    /// This is used to differentiate between a standard game and a puzzle challenge, where the player may have specific objectives or constraints.
    /// </summary>
    private bool _isPuzzleMode = false;

    /// <summary>
    /// Stores the current puzzle response from Lichess.
    /// This is used to manage the state of the puzzle, including the current position, moves, and any additional information provided by the Lichess API.
    /// </summary>
    private LichessPuzzleResponse _currentPuzzleResponse;

    /// <summary>
    /// Stores the solutions for the current puzzle.
    /// This is used to validate the player's moves against the expected solutions for the puzzle.
    /// </summary>
    private string[] _puzzleSolutions;

    /// <summary>
    /// Stores the index of the current puzzle solution.
    /// This is used to track the player's progress through the puzzle solutions, allowing them to advance or retry as needed.
    /// </summary>
    private int _currentPuzzleSolutionIndex = 0;

    /// <summary>
    /// Stores the history of moves made in the game.
    /// This is used to keep track of the moves for Stockfish analysis and game state management.
    /// </summary>
    private List<string> _moveHistory = new List<string>();

    private void Start()
    {
        StockfishController.Instance.OnBestMoveFound += AIMakeMove;
    }

    void OnDestroy()
    {
        StockfishController.Instance.OnBestMoveFound -= AIMakeMove;
    }

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

        _isPuzzleMode = false; // Reset puzzle mode
        _isStockFishAllowed = true; // Reset Stockfish allowance

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

        // Notify Stockfish of the move history
        StockfishController.Instance.SetPositionWithMoves(_moveHistory);

        // Request Stockfish to find the best move for the next turn
        StockfishController.Instance.FindBestMove();


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

        ReadOnlyCollection<DetailedMove> moves = _game.Moves;
        foreach (var move in moves)
        {
            // Store the move in history
            string from = move.OriginalPosition.ToString();
            string to = move.NewPosition.ToString();
            char? promotionPiece = move.Promotion;
            _moveHistory.Add($"{from}{to}{(promotionPiece.HasValue ? promotionPiece.Value.ToString() : string.Empty)}".ToLower());
        }

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
    /// Starts a new game using a Lichess puzzle response.
    /// This method extracts the PGN from the Lichess puzzle response and initializes a new game with it.
    /// </summary>
    /// <param name="lichessPuzzleResponse"></param>
    public void StartGame(LichessPuzzleResponse lichessPuzzleResponse)
    {
        // Extract the PGN from the response
        string pgn = lichessPuzzleResponse.game.pgn;

        // Start a new game with the provided PGN
        StartGame(pgn);

        _currentPuzzleSolutionIndex = 0; // Reset the current puzzle solution index
        _isStockFishAllowed = false; // Disable Stockfish for puzzles
        _isPuzzleMode = true; // Set puzzle mode to true
        _aiSide = _game.WhoseTurn == ChessDotNet.Player.White ? ChessDotNet.Player.Black : ChessDotNet.Player.White; // Set AI side based on the current turn

        // Store the current puzzle response
        _currentPuzzleResponse = lichessPuzzleResponse;

        // Store the puzzle solutions
        _puzzleSolutions = lichessPuzzleResponse.puzzle.solution;
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

            // If in puzzle mode, check if the move matches the current puzzle solution
            if(_isPuzzleMode && _currentPlayer != _aiSide)
                ValidatePlayerMoveInPuzzleMode(moveString.ToLower());

            // Store the move in history
            _moveHistory.Add(moveString.ToLower());

            // Stop Stockfish's current search if it is running
            //StockfishController.Instance.StopFindBestMoveInfinite();

            // Notify Stockfish of the move history
            StockfishController.Instance.SetPositionWithMoves(_moveHistory);

            // Request Stockfish to find the best move for the next turn
            StockfishController.Instance.FindBestMove();
            //StockfishController.Instance.FindBestMoveInfinite();

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

    private void ValidatePlayerMoveInPuzzleMode(string moveString)
    {
        if (!_isPuzzleMode)
        {
            Debug.LogWarning("Not in puzzle mode");
            return;
        }

        if (_currentPuzzleSolutionIndex >= _puzzleSolutions.Length)
        {
            Debug.LogWarning("No more solutions to check in the current puzzle.");
            return;
        }

        // Check if the move matches the current puzzle solution
        if (moveString.Equals(_puzzleSolutions[_currentPuzzleSolutionIndex]))
        {
            _currentPuzzleSolutionIndex++;
            if (_currentPuzzleSolutionIndex >= _puzzleSolutions.Length)
            {
                Debug.Log("Puzzle completed!");
                // Notify listeners that the puzzle is completed
                // EventBus<PuzzleCompletedEvent>.Raise(new PuzzleCompletedEvent());
                return;
            }
            
            Invoke(nameof(AIPuzzleMakeMove), 1f); // Reload the game after a short delay to allow UI updates
        }
        else
        {
            Debug.LogWarning($"Incorrect move: {moveString}. Expected: {_puzzleSolutions[_currentPuzzleSolutionIndex]}");
            // Optionally, notify listeners of the incorrect move
            //EventBus<IncorrectMoveEvent>.Raise(new IncorrectMoveEvent { Move = moveString });
        }
    }

    public void AIMakeMove(string input)
    {
        if (_aiSide != _game.WhoseTurn)
        {
            Debug.LogWarning($"It's not {_aiSide}'s turn to play.");
            return;
        }

        if (_isStockFishAllowed == false)
        {
            Debug.LogWarning("AI is not allowed to make a move at this time.");
            return;
        }

        string from = input.Substring(0, 2);
        string to = input.Substring(2, 2);
        char? promotionPiece = null;
        if (input.Length > 4)
        {
            promotionPiece = input[4];
        }

        MoveType moveType = MakeMove(from, to, promotionPiece);
        if (moveType != MoveType.Invalid)
        {
            ReloadGame();
        }
    }

    public void AIPuzzleMakeMove()
    {
        if (_isPuzzleMode == false)
        {
            Debug.LogWarning("Not in puzzle mode. AI cannot make a move.");
            return;
        }

        if (_currentPuzzleSolutionIndex >= _puzzleSolutions.Length)
        {
            Debug.LogWarning("No more solutions to check in the current puzzle.");
            return;
        }

        string input = _puzzleSolutions[_currentPuzzleSolutionIndex];
        string from = input.Substring(0, 2);
        string to = input.Substring(2, 2);
        char? promotionPiece = null;
        MoveType moveType = MakeMove(from, to, promotionPiece);
        if (moveType != MoveType.Invalid)
        {
            _currentPuzzleSolutionIndex++;
            ReloadGame();

            if (_currentPuzzleSolutionIndex >= _puzzleSolutions.Length)
            {
                Debug.Log("Puzzle completed!");
                // Notify listeners that the puzzle is completed
                // EventBus<PuzzleCompletedEvent>.Raise(new PuzzleCompletedEvent());
            }
        }
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
