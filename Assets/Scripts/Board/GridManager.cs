using UnityEngine;
using UnityEngine.UI;

public class GridManager : MonoBehaviour
{
    [Header("Prefabs"), Space(10)]
    [SerializeField] private Cell _cellPrefab;
    [SerializeField] private IndicatorText _indicatorTextFile;
    [SerializeField] private IndicatorText _indicatorTextRank;

    [Header("Layout Groups"), Space(10)]
    [SerializeField] private GridLayoutGroup _boardLayoutGroup;
    [SerializeField] private GridLayoutGroup _rankLayoutGroup;
    [SerializeField] private GridLayoutGroup _fileLayoutGroup;

    private Cell[,] _grid;

    private EventBinding<ClearBoardEvent> _clearBoardEventBinding;
    private EventBinding<NewGameEvent> _newGameEventBinding;
    private EventBinding<ReloadBoardEvent> _reloadBoardEventBinding;

    void OnEnable()
    {
        _newGameEventBinding = new EventBinding<NewGameEvent>(OnNewGame);
        EventBus<NewGameEvent>.Register(_newGameEventBinding);

        _reloadBoardEventBinding = new EventBinding<ReloadBoardEvent>(OnReloadBoard);
        EventBus<ReloadBoardEvent>.Register(_reloadBoardEventBinding);

        _clearBoardEventBinding = new EventBinding<ClearBoardEvent>(ClearGrid);
        EventBus<ClearBoardEvent>.Register(_clearBoardEventBinding);
    }

    void OnDisable()
    {
        EventBus<NewGameEvent>.Deregister(_newGameEventBinding);
        EventBus<ReloadBoardEvent>.Deregister(_reloadBoardEventBinding);
        EventBus<ClearBoardEvent>.Deregister(_clearBoardEventBinding);
    }

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

        _boardLayoutGroup.cellSize = cellSizeVector;
        _rankLayoutGroup.cellSize = cellSizeVector;
        _fileLayoutGroup.cellSize = cellSizeVector;

        // Generate board cells
        char[] files = ConstantData.FILES;

        for (int i = 0; i < GRIDSIZE; i++)
        {
            for (int j = 0; j < GRIDSIZE; j++)
            {
                Cell cell = Instantiate(_cellPrefab, _boardLayoutGroup.transform);
                cell.CellName = $"{files[j]}{1 + i}";
                _grid[i, j] = cell;

                // Set cell color
                cell.SetCellColor((i + j) % 2 == 0);
                cell.CellPosition = new Vector2Int(j + 1, i + 1); // Set cell position in the grid
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

    private void OnNewGame(NewGameEvent newGameEvent)
    {
        // Create a solution from the FEN notation if provided
        FenNotationToGridBoard(newGameEvent.Fen);

        if (newGameEvent.IsWhiteSide)
        {

        }
    }

    private void FenNotationToGridBoard(string fen)
    {
        // This method can be implemented to create a solution from the FEN notation    
        Debug.Log($"Creating solution from FEN: {fen}");

        string[] sections = fen.Split(' ');
        string piecePlacement = sections[0];
        string[] rows = piecePlacement.Split('/');

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
                        Cell cell = _grid[x, y];
                        Image pieceImage = PieceManager.Instance.GetPieceImage(type, color, cell.transform);
                        cell.SetPiece(pieceImage);
                    }

                    file++;
                }
            }
        }
    }

    public void ClearGrid()
    {
        foreach (Cell cell in _grid)
        {
            cell.RemovePiece();
        }
    }

    private void OnReloadBoard(ReloadBoardEvent reloadBoardEvent)
    {
        ClearGrid();
        FenNotationToGridBoard(reloadBoardEvent.Fen);
    }
}
