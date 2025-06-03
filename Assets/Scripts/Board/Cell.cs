using ChessDotNet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cell is a MonoBehaviour that represents a single cell on the chess board.
/// It handles the piece placement, cell color, and user interactions such as selecting and moving pieces.
/// </summary>
public class Cell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler
{
    [SerializeField] private Image _cellBackground;

    [SerializeField] private Image _piece;

    /// <summary>
    /// The type of piece currently placed in this cell.
    /// This property is used to identify the piece type for game logic and rendering.
    /// </summary>
    private PieceType _pieceType;
    public PieceType PieceType
    {
        get => _pieceType;
        set => _pieceType = value;
    }

    /// <summary>
    /// The position of the cell on the chess board, represented as a Vector2Int.
    /// This property is used to identify the cell's position for game logic and rendering.
    /// </summary>
    private Vector2Int _cellPosition;
    public Vector2Int CellPosition
    {
        get => _cellPosition;
        set => _cellPosition = value;
    }

    /// <summary>
    /// The color of the piece currently placed in this cell.
    /// This property is used to identify the piece color for game logic and rendering.
    /// </summary>
    private bool _isLightSquare;
    public bool IsLightSquare
    {
        get => _isLightSquare;
    }

    /// <summary>
    /// The name of the cell, typically represented as a string in chess notation (e.g., "a1", "h8").
    /// This property is used to identify the cell for game logic and rendering.
    /// </summary>
    private string _cellName;
    public string CellName
    {
        get => _cellName;
        set => _cellName = value;
    }

    /// <summary>
    /// Indicates whether the cell is empty (i.e., has no piece placed in it).
    /// This property is used to check if the cell can accept a piece or if it is available for moves.
    /// </summary>
    public bool IsEmpty
    {
        get => _piece == null;
    }

    /// <summary>
    /// Sets the color of the cell based on whether it is an odd or even square.
    /// This method updates the cell's background color to either a light or dark color based on the square's position.
    /// </summary>
    /// <param name="isOdd"></param>
    /// <param name="isSelected"></param>
    public void SetCellColor(bool isOdd, bool isSelected = false)
    {
        if (isSelected)
        {
            _cellBackground.color = isOdd ? ConstantData.CELL_DARK_COLOR_SELECT : ConstantData.CELL_LIGHT_COLOR_SELECT;
        }
        else
        {
            _cellBackground.color = isOdd ? ConstantData.CELL_DARK_COLOR : ConstantData.CELL_LIGHT_COLOR;
        }
        _isLightSquare = !isOdd;
    }

    /// <summary>
    /// Sets the cell color to indicate selection.
    /// This method updates the cell's background color to a selected state based on whether it is a light or dark square.
    /// </summary>
    public void SetCellColorOnSelect()
    {
        _cellBackground.color = _isLightSquare ? ConstantData.CELL_LIGHT_COLOR_SELECT : ConstantData.CELL_DARK_COLOR_SELECT;
    }

    /// <summary>
    /// Sets the cell color to indicate deselection.
    /// This method updates the cell's background color back to its original state based on whether it is a light or dark square.
    /// </summary>
    public void SetCellColorOnDeselect()
    {
        _cellBackground.color = _isLightSquare ? ConstantData.CELL_LIGHT_COLOR : ConstantData.CELL_DARK_COLOR;
    }

    /// <summary>
    /// Sets the piece in this cell.
    /// This method updates the cell to contain a piece image, setting its parent, position, scale, and layout properties.
    /// </summary>
    /// <param name="piece"></param>
    public void SetPiece(Image piece)
    {
        if (piece != null)
        {
            _piece = piece;
            _piece.transform.SetParent(transform); // Set parent and maintain local scale
            _piece.transform.localPosition = Vector3.zero; // Reset position to avoid positioning issues
            _piece.transform.localScale = Vector3.one * 0.7f; // Reset scale to avoid scaling issues
            _piece.rectTransform.offsetMax = Vector2.zero; // Reset offset to avoid layout issues
            _piece.rectTransform.offsetMin = Vector2.zero; // Reset offset to avoid layout issues
            _piece.rectTransform.anchorMax = Vector2.one; // Reset anchor to avoid layout issues
            _piece.rectTransform.anchorMin = Vector2.zero; // Reset anchor to avoid layout issues
        }
        else
        {
            _piece = null;
        }
    }

    /// <summary>
    /// Removes the piece from this cell.
    /// This method deactivates the piece image, resets its parent, and clears the piece reference.
    /// </summary>
    public void RemovePiece()
    {
        if (_piece != null)
        {
            PieceManager.Instance.ReturnPieceImage(_piece);
            _piece = null; // Clear the piece reference
            _pieceType = PieceType.None; // Reset piece type         
        }
    }

    /// <summary>
    /// Static references to the FROM_CELL and TO_CELL.
    /// These references are used to track the selected cells during piece movement.
    /// </summary>
    public static Cell FROM_CELL = null;
    public static Cell TO_CELL = null;


    /// <summary>
    /// Handles mouse enter events on the cell.
    /// This method is called when the pointer enters the cell, allowing it to highlight the cell as a potential target for piece movement.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (FROM_CELL != null) // => selecting TO_CELL
        {
            if (TO_CELL != null)
            {
                // DeHighlight previous TO_CELL
                TO_CELL.SetCellColorOnDeselect();
            }

            TO_CELL = this;
            // Highlight new TO_CELL        
            // Since TO_CELL is in current script,
            // we can directly call SetCellColorOnSelect instead of using TO_CELL.SetCellColorOnSelect();
            SetCellColorOnSelect();
        }
    }

    /// <summary>
    /// Handles mouse down events on the cell.
    /// This method is called when the pointer is pressed down on the cell, allowing it to select or deselect cells and move pieces accordingly.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (FROM_CELL == null && _piece != null) // => selecting FROM_CELL
        {
            FROM_CELL = this;
            // Highlight FROM_CELL
            // Since FROM_CELL is in current script,
            // we can directly call SetCellColorOnSelect instead of using FROM_CELL.SetCellColorOnSelect();
            SetCellColorOnSelect();
        }
        else if (TO_CELL != null && FROM_CELL != this) // => selecting TO_CELL and moving piece
        {
            MovePiece(FROM_CELL, TO_CELL);

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL

            FROM_CELL = TO_CELL = null; // Reset after move
        }
        else if (FROM_CELL == this) // => deselecting FROM_CELL because it is already selected
        {
            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            FROM_CELL = null;
        }
    }

    /// <summary>
    /// Handles mouse up events on the cell.
    /// This method is called when the pointer is released over the cell, allowing it to finalize piece movement or deselect cells.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (FROM_CELL == TO_CELL && FROM_CELL != null) // => deselecting FROM_CELL or no piece moved
        {
            SetPiece(_piece); // Reattach piece to cell

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL

            FROM_CELL = TO_CELL = null; // Reset FROM_CELL if not moved
        }
        else if (TO_CELL != null && FROM_CELL != null && FROM_CELL != TO_CELL)
        {
            MovePiece(FROM_CELL, TO_CELL);

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL

            FROM_CELL = TO_CELL = null; // Reset after move
        }
        else if (TO_CELL == null && FROM_CELL != null)
        {
            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL if TO_CELL is null
            FROM_CELL = null; // Reset FROM_CELL if no move occurred
            SetPiece(_piece);
        }
    }

    /// <summary>
    /// Handles the beginning of a drag event on the cell.
    /// This method is called when the pointer starts dragging a piece, allowing it to detach the piece from the cell for movement.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (FROM_CELL != null && _piece != null)
        {
            // Detach piece from cell to allow dragging
            _piece.transform.SetParent(this.transform.parent.transform.parent);
        }
    }

    /// <summary>
    /// Handles the dragging of a piece over the cell.
    /// This method is called continuously while the pointer is dragging a piece, allowing it to update the piece's position to follow the mouse cursor.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnDrag(PointerEventData eventData)
    {
        if (_piece != null)
            // Move the piece with the mouse
            _piece.transform.position = eventData.position;
    }

    /// <summary>
    /// Moves a piece from one cell to another.
    /// This method handles the logic for moving a piece between cells, including special cases like castling, en passant, and promotion.
    /// </summary>
    /// <param name="fromCell"></param>
    /// <param name="toCell"></param>
    public static void MovePiece(Cell fromCell, Cell toCell)
    {
        if (fromCell != null && toCell != null && fromCell._piece != null)
        {
            char? promotionPiece = null;
            // Check if current piece is a pawn and if it reaches the promotion rank
            // Assuming promotion rank is 1 for White and 8 for Black
            if (fromCell.PieceType == PieceType.Pawn && (toCell.CellPosition.y == 1 || toCell.CellPosition.y == 8))
            {
                // Handle pawn promotion logic here if needed
                Debug.Log($"Pawn promotion logic can be handled here for {fromCell.CellName} to {toCell.CellName}");
                promotionPiece = 'Q'; // Example: promote to Queen
            }

            // Make a move in ChessGame Instance and return the MoveType
            MoveType moveType = GameManager.Instance.MakeMove(fromCell.CellName, toCell.CellName, promotionPiece);

            if (moveType.HasFlag(MoveType.EnPassant))
            {
                GameManager.Instance.ReloadGame(); // Reload the game to update the board after castling
            }
            else if (moveType.HasFlag(MoveType.Promotion))
            {
                GameManager.Instance.ReloadGame(); // Reload the game to update the board after castling
            }
            else if (moveType.HasFlag(MoveType.Castling))
            {
                GameManager.Instance.ReloadGame(); // Reload the game to update the board after castling
            }
            else if (moveType.HasFlag(MoveType.Capture))
            {
                // Remove captured piece from TO_CELL
                toCell.RemovePiece();

                // Move the current piece to TO_CELL
                toCell.SetPiece(fromCell._piece);

                // Clear the piece from FROM_CELL
                fromCell.SetPiece(null);
            }
            else if (moveType == MoveType.Move)
            {
                // Move the current piece to TO_CELL
                toCell.SetPiece(fromCell._piece);

                // Clear the piece from FROM_CELL
                fromCell.SetPiece(null);
            }
            else if (moveType == MoveType.Invalid)
            {
                // Reattach piece to FROM_CELL if move is invalid
                fromCell.SetPiece(fromCell._piece);
            }
        }
        else
        {
            Debug.LogWarning("Invalid move: either FROM_CELL or TO_CELL is null or has no piece.");
        }
    }
}
