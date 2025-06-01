using ChessDotNet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Cell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler
{
    [SerializeField] private Image _cellBackground;

    [SerializeField] private Image _piece;

    private PieceType _pieceType;
    public PieceType PieceType
    {
        get => _pieceType;
        set => _pieceType = value;
    }

    private Vector2Int _cellPosition;
    public Vector2Int CellPosition
    {
        get => _cellPosition;
        set => _cellPosition = value;
    }

    private bool _isLightSquare;
    public bool IsLightSquare
    {
        get => _isLightSquare;
    }

    private string _cellName;
    public string CellName
    {
        get => _cellName;
        set => _cellName = value;
    }

    public bool IsEmpty
    {
        get => _piece == null;
    }

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

    public void SetCellColorOnSelect()
    {
        _cellBackground.color = _isLightSquare ? ConstantData.CELL_LIGHT_COLOR_SELECT : ConstantData.CELL_DARK_COLOR_SELECT;
    }

    public void SetCellColorOnDeselect()
    {
        _cellBackground.color = _isLightSquare ? ConstantData.CELL_LIGHT_COLOR : ConstantData.CELL_DARK_COLOR;
    }

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

    public void RemovePiece()
    {
        if (_piece != null)
        {
            PieceManager.Instance.ReturnPieceImage(_piece);
            _piece = null; // Clear the piece reference
            _pieceType = PieceType.None; // Reset piece type         
        }
    }

    public static Cell FROM_CELL = null;
    public static Cell TO_CELL = null;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (FROM_CELL != null) // => selecting TO_CELL
        {
            //Debug.Log($"Selected target cell: {name}");

            if (TO_CELL != null)
            {
                TO_CELL.SetCellColorOnDeselect(); // DeHighlight previous TO_CELL
            }

            TO_CELL = this;
            SetCellColorOnSelect(); // Highlight FROM_CELL
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (FROM_CELL == null && _piece != null) // => selecting FROM_CELL
        {
            //Debug.Log($"Selected cell: {name}");
            FROM_CELL = this;
            SetCellColorOnSelect(); // Highlight FROM_CELL
        }
        else if (TO_CELL != null && FROM_CELL != this) // => selecting TO_CELL
        {
            // Trigger move piece logic here
            // MovePiece(FROM_CELL, TO_CELL);
            MovePiece(FROM_CELL, TO_CELL);

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL
            
            FROM_CELL = TO_CELL = null; // Reset after move
        }
        else if (FROM_CELL == this) // => deselecting FROM_CELL
        {
            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            FROM_CELL = null;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (FROM_CELL == TO_CELL && FROM_CELL != null) // => deselecting FROM_CELL or no piece moved
        {
            //Debug.Log($"Deselected cell: {name}");
            SetPiece(_piece); // Reattach piece to cell

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL

            FROM_CELL = TO_CELL = null; // Reset FROM_CELL if not moved
        }
        else if (TO_CELL != null && FROM_CELL != TO_CELL)
        {
            // Trigger move piece logic here
            MovePiece(FROM_CELL, TO_CELL);

            FROM_CELL.SetCellColorOnDeselect(); // DeHighlight FROM_CELL
            TO_CELL.SetCellColorOnDeselect(); // DeHighlight TO_CELL

            FROM_CELL = TO_CELL = null; // Reset after move
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (FROM_CELL != null && _piece != null)
        {
            _piece.transform.SetParent(this.transform.parent.transform.parent); // Detach piece from cell to allow dragging
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        _piece.transform.position = eventData.position; // Move the piece with the mouse
    }
    
    public static void MovePiece(Cell fromCell, Cell toCell)
    {
        if (fromCell != null && toCell != null && fromCell._piece != null)
        {
            char? promotionPiece = null;
            if (fromCell.PieceType == PieceType.Pawn && (toCell.CellPosition.y == 1 || toCell.CellPosition.y == 8))
            {
                // Handle pawn promotion logic here if needed
                Debug.Log($"Pawn promotion logic can be handled here for {fromCell.CellName} to {toCell.CellName}");
                promotionPiece = 'Q'; // Example: promote to Queen
            }
            MoveType moveType = GameManager.Instance.MakeMove(fromCell.CellName, toCell.CellName,promotionPiece);
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
                toCell.RemovePiece(); // Remove captured piece from TO_CELL
                toCell.SetPiece(fromCell._piece);
                fromCell.SetPiece(null);
            }
            else if (moveType == MoveType.Move)
            {
                toCell.SetPiece(fromCell._piece);
                fromCell.SetPiece(null);
            }
            else if (moveType == MoveType.Invalid)
            {
                fromCell.SetPiece(fromCell._piece); // Reattach piece to FROM_CELL if move is invalid
            }
        }
        else
        {
            Debug.LogWarning("Invalid move: either FROM_CELL or TO_CELL is null or has no piece.");
        }
    }
}
