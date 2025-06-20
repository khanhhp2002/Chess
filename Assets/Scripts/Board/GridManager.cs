using System;
using System.Collections;
using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GridManager is a MonoBehaviour that manages the chess board user interface.
/// </summary>
public class GridManager : Singleton<GridManager>
{
    [Header("Prefabs"), Space(10)]
    [SerializeField] private Cell _cellPrefab;
    [SerializeField] private IndicatorText _indicatorTextFile;
    [SerializeField] private IndicatorText _indicatorTextRank;

    [Header("Layout Groups"), Space(10)]
    [SerializeField] private GridLayoutGroup _boardLayoutGroup;
    [SerializeField] private GridLayoutGroup _rankLayoutGroup;
    [SerializeField] private GridLayoutGroup _fileLayoutGroup;

    [Header("Evaluation charts"), Space(10)]
    [SerializeField] private Image _evaluationChart;

    [Header("Player Info"), Space(10)]
    [SerializeField] private PlayerInfo _playerInfoTop;
    [SerializeField] private PlayerInfo _playerInfoBottom;

    [SerializeField] private BoardLoadingCover _boardLoadingCover;
    [SerializeField] private TMP_Text _hintText;

    [SerializeField] private CanvasGroup _boardCanvasGroup;


    private Cell[,] _grid;

    private EventBinding<ClearBoardEvent> _clearBoardEventBinding;
    private EventBinding<NewGameEvent> _newGameEventBinding;
    private EventBinding<ReloadBoardEvent> _reloadBoardEventBinding;
    private EventBinding<CurrentPlayerChangedEvent> _currentPlayerChangedEventBinding;
    private EventBinding<GameEndEvent> _gameEndEventBinding;
    private EventBinding<NewPuzzleGameEvent> _newPuzzleGameEventBinding;

    /// <summary>
    /// Called when the script instance is being loaded.
    /// This method registers event bindings for new game, reload board, and clear board events.
    /// </summary>
    void OnEnable()
    {
        _newGameEventBinding = new EventBinding<NewGameEvent>(OnNewGame);
        EventBus<NewGameEvent>.Register(_newGameEventBinding);

        _reloadBoardEventBinding = new EventBinding<ReloadBoardEvent>(OnReloadBoard);
        EventBus<ReloadBoardEvent>.Register(_reloadBoardEventBinding);

        _clearBoardEventBinding = new EventBinding<ClearBoardEvent>(ClearGrid);
        EventBus<ClearBoardEvent>.Register(_clearBoardEventBinding);

        _currentPlayerChangedEventBinding = new EventBinding<CurrentPlayerChangedEvent>(OnPlayerTurnChanged);
        EventBus<CurrentPlayerChangedEvent>.Register(_currentPlayerChangedEventBinding);

        _gameEndEventBinding = new EventBinding<GameEndEvent>(OnGameEnd);
        EventBus<GameEndEvent>.Register(_gameEndEventBinding);

        _newPuzzleGameEventBinding = new EventBinding<NewPuzzleGameEvent>(OnNewPuzzleGame);
        EventBus<NewPuzzleGameEvent>.Register(_newPuzzleGameEventBinding);

        StockfishTcpClient.Instance.OnPositionEvaluated += UpdateEvaluationChart;
    }

    /// <summary>
    /// Called when the script instance is being disabled.
    /// This method deregisters the event bindings to prevent memory leaks and unwanted behavior.
    /// </summary>
    void OnDisable()
    {
        EventBus<NewGameEvent>.Deregister(_newGameEventBinding);
        EventBus<ReloadBoardEvent>.Deregister(_reloadBoardEventBinding);
        EventBus<ClearBoardEvent>.Deregister(_clearBoardEventBinding);
        EventBus<CurrentPlayerChangedEvent>.Deregister(_currentPlayerChangedEventBinding);

        StockfishTcpClient.Instance.OnPositionEvaluated -= UpdateEvaluationChart;
    }

    /// <summary>
    /// Initializes the grid and generates the chess board UI elements.
    /// This method sets up the grid size, cell size, and generates the board cells, rank indicators, and file indicators.
    /// </summary>
    private void Start()
    {
        // Init grid
        int GRIDSIZE = ConstantData.GRID_SIZE;
        _grid = new Cell[GRIDSIZE, GRIDSIZE];

        // Get UI Size
        RectTransform rectTransform = GetComponent<RectTransform>();
        float boardSize = rectTransform.rect.width;

        // Set cell size
        float cellSize = boardSize / GRIDSIZE;
        Vector2 cellSizeVector = new Vector2(cellSize, cellSize);

        // Set layout groups cell size
        _boardLayoutGroup.cellSize = cellSizeVector;
        _rankLayoutGroup.cellSize = cellSizeVector;
        _fileLayoutGroup.cellSize = cellSizeVector;

        // Generate board cells
        char[] files = ConstantData.FILES;

        for (int i = 0; i < GRIDSIZE; i++)
        {
            for (int j = 0; j < GRIDSIZE; j++)
            {
                // Instantiate cell
                Cell cell = Instantiate(_cellPrefab, _boardLayoutGroup.transform);

                // Set cell properties
                cell.CellName = $"{files[j]}{1 + i}";

                // Add cell to grid
                _grid[i, j] = cell;

                // Set cell color
                cell.SetCellColor((i + j) % 2 == 0);

                // Set cell position
                cell.CellPosition = new Vector2Int(j + 1, i + 1);
            }
        }

        // Generate rank indicators
        for (int i = 0; i < GRIDSIZE; i++)
        {
            IndicatorText rankIndicator = Instantiate(_indicatorTextRank, _rankLayoutGroup.transform);
            rankIndicator.SetText((i + 1).ToString());
            rankIndicator.SetTextColor(i % 2 == 1);
        }

        // Generate file indicators
        for (int j = 0; j < GRIDSIZE; j++)
        {
            IndicatorText fileIndicator = Instantiate(_indicatorTextFile, _fileLayoutGroup.transform);
            fileIndicator.SetText(files[j].ToString());
            fileIndicator.SetTextColor(j % 2 == 1);
        }
    }


    /// <summary>
    /// Handles the NewGameEvent to create a new game board.
    /// This method initializes the chess board based on the provided FEN notation and sets up the initial game state.
    /// </summary>
    /// <param name="newGameEvent"></param>
    private void OnNewGame(NewGameEvent newGameEvent)
    {
        // Create a solution from the FEN notation if provided
        FenNotationToGridBoard(newGameEvent.Fen);

        _playerInfoTop.SetPlayerInfo(true);
        _playerInfoBottom.SetPlayerInfo(false);

        // Flip the board if the player is on the black side
        if (newGameEvent.IsWhiteSide)
        {

        }
    }

    /// <summary>
    /// Handles the NewPuzzleGameEvent to set up a new puzzle game.
    /// This method updates the player information UI and displays the loading cover with puzzle details.
    /// </summary>
    /// <param name="newPuzzleGameEvent"></param>
    private void OnNewPuzzleGame(NewPuzzleGameEvent newPuzzleGameEvent)
    {
        _playerInfoBottom.SetPlayerInfo(newPuzzleGameEvent.whitePlayerName,
            int.Parse(newPuzzleGameEvent.whitePlayerElo),
            null);
        _playerInfoTop.SetPlayerInfo(newPuzzleGameEvent.blackPlayerName,
            int.Parse(newPuzzleGameEvent.blackPlayerElo),
            null);

        string[] themes = newPuzzleGameEvent.themes;
        string tags = string.Empty;
        string mateInXMoves = string.Empty;
        foreach (string theme in themes)
        {
            Debug.Log($"Theme: {theme}");
            if (theme.ToLower().Contains("matein"))
            {
                mateInXMoves = $"Mate in {theme.Substring(6)}";
                continue;
            }
            tags += $"#{theme} ";
        }
        string description = string.IsNullOrEmpty(mateInXMoves) ? tags : $"<u>{mateInXMoves}</u>\n\n{tags}";
        _boardLoadingCover.TurnOff(
            $"Puzzle: {newPuzzleGameEvent.whitePlayerName} vs {newPuzzleGameEvent.blackPlayerName}",
            description);

        string solutionString = string.Join(" - ", newPuzzleGameEvent.solution);
        _hintText.text = $"{solutionString}"; 
        _hintText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Displays the hint text for the puzzle solution.
    /// This method activates the hint text UI element to show the solution to the puzzle.
    /// </summary>
    public void ShowHintText()
    {
        if (GameManager.Instance.HasHint())
            _hintText.gameObject.SetActive(true);
        else
        {
            GameManager.Instance.OpenNotificationWindow("No Puzzle in Progress",
                    "Please start a new game first", () => GameManager.Instance.StartGameOrPuzzle(true), null);
        }
    }

    /// <summary>
    /// Turns on the board loading cover.
    /// This method activates the loading cover UI element to indicate that the board is being set up.
    /// </summary>
    public void TurnOnBoardLoadingCover()
    {
        _boardLoadingCover.TurnOn();
    }

    /// <summary>
    /// Converts FEN notation to a grid board representation.
    /// This method parses the FEN string to set up the chess pieces on the grid board.
    /// </summary>
    /// <param name="fen"></param>
    private void FenNotationToGridBoard(string fen)
    {
        // Fen example: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        Debug.Log($"Creating solution from FEN: {fen}");

        // Sections: | "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR" | "w" | "KQkq" | "-" | "0" | "1" |
        string[] sections = fen.Split(' ');

        // First section: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
        string piecePlacement = sections[0];

        // Rows: | "rnbqkbnr" | "pppppppp" | "8" | "8" | "8" | "8" | "PPPPPPPP" | "RNBQKBNR" |
        string[] rows = piecePlacement.Split('/');

        // Validate the number of rows
        if (rows.Length != 8)
        {
            Debug.LogError("Invalid FEN: Expected 8 ranks.");
            return;
        }

        for (int rank = 0; rank < ConstantData.GRID_SIZE; rank++)
        {
            string row = rows[rank];
            int file = 0;

            foreach (char symbol in row)
            {
                if (char.IsDigit(symbol))
                {
                    file += (int)char.GetNumericValue(symbol); // Skip empty squares
                }
                else
                {
                    PieceColor color = char.IsUpper(symbol) ? PieceColor.White : PieceColor.Black;
                    PieceType type;

                    // Convert the symbol to a PieceType
                    // Use char.ToLower to handle both upper and lower case symbols
                    switch (char.ToLower(symbol))
                    {
                        case 'p': type = PieceType.Pawn; break;
                        case 'r': type = PieceType.Rook; break;
                        case 'n': type = PieceType.Knight; break;
                        case 'b': type = PieceType.Bishop; break;
                        case 'q': type = PieceType.Queen; break;
                        case 'k': type = PieceType.King; break;
                        default:
                            Debug.LogWarning($"Unrecognized piece: {symbol}");
                            file++;
                            continue;
                    }

                    // x is rank from 0 (bottom) to 7 (top)
                    int x = 7 - rank;
                    int y = file;

                    if (x < 0 || x >= 8 || y < 0 || y >= 8)
                    {
                        Debug.LogWarning($"Position out of range: x={x}, y={y}");
                    }
                    else
                    {
                        // Set the piece in the grid
                        Cell cell = _grid[x, y];

                        //Get chess piece image
                        Image pieceImage = PieceManager.Instance.GetPieceImage(type, color);

                        // Set the piece in the cell
                        cell.SetPiece(pieceImage);

                        // Set the piece type
                        cell.PieceType = type;
                    }

                    file++;
                }
            }
        }
    }

    /// <summary>
    /// Clears the chess board grid.
    /// This method removes all pieces from the grid cells, effectively resetting the board.
    /// </summary>
    public void ClearGrid()
    {
        foreach (Cell cell in _grid)
        {
            cell.RemovePiece();
        }
    }

    /// <summary>
    /// Handles the ReloadBoardEvent to reload the chess board with a new FEN notation.
    /// This method clears the current grid and sets up the board based on the provided FEN string.
    /// </summary>
    /// <param name="reloadBoardEvent"></param>
    private void OnReloadBoard(ReloadBoardEvent reloadBoardEvent)
    {
        ClearGrid();
        FenNotationToGridBoard(reloadBoardEvent.Fen);
    }

    private Coroutine _evaluationChartCoroutine;

    public void UpdateEvaluationChart(float value)
    {
        if (_evaluationChartCoroutine != null)
        {
            StopCoroutine(_evaluationChartCoroutine);
        }

        // Start the coroutine to update the evaluation chart
        _evaluationChartCoroutine = StartCoroutine(UpdateEvaluationChartCoroutine(value));
    }

    IEnumerator UpdateEvaluationChartCoroutine(float value)
    {
        // Ensure the value is clamped between 0 and 1
        float t = 1f;
        float startValue = _evaluationChart.fillAmount;
        float endValue = value;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            _evaluationChart.fillAmount = Mathf.Lerp(startValue, endValue, 1f - t);
            yield return null;
        }
    }

    /// <summary>
    /// Handles the CurrentPlayerChangedEvent to update the player information UI.
    /// This method updates the player info based on the current player and whether they are a bot or a human player.
    /// </summary>
    /// param name="currentPlayerChangedEvent"></param>
    private void OnPlayerTurnChanged(CurrentPlayerChangedEvent currentPlayerChangedEvent)
    {
        ChessDotNet.Player currentPlayer = currentPlayerChangedEvent.CurrentPlayer;
        if (currentPlayer == GameManager.Instance.GetAIPlayerSide())
        {
            _boardCanvasGroup.interactable = false;
        }
        else
        {
            _boardCanvasGroup.interactable = true;
        }

        if (currentPlayer == ChessDotNet.Player.White)
        {
            _playerInfoTop.FadePlayerInfo();
            _playerInfoBottom.ShowPlayerInfo();
        }
        else
        {
            _playerInfoTop.ShowPlayerInfo();
            _playerInfoBottom.FadePlayerInfo();
        }
    }

    private void OnGameEnd(GameEndEvent gameEndEvent)
    {
        // Hide player info when the game ends
        _playerInfoTop.FadePlayerInfo();
        _playerInfoBottom.FadePlayerInfo();

        // Optionally, you can display a message or handle the game end logic here
        if (gameEndEvent.IsWhiteWinner)
        {
            _playerInfoBottom.ShowPlayerInfo();
            _playerInfoTop.FadePlayerInfo();
            _playerInfoBottom.SetCrownVisibility(true);
        }
        else if (gameEndEvent.IsBlackWinner)
        {
            _playerInfoTop.ShowPlayerInfo();
            _playerInfoBottom.FadePlayerInfo();
            _playerInfoTop.SetCrownVisibility(true);
        }
    }
    
    public PlayerInfo PlayerInfoTop => _playerInfoTop;
    public PlayerInfo PlayerInfoBottom => _playerInfoBottom;
}
