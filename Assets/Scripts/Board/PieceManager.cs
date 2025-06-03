using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PieceManager is a singleton class that manages chess piece images.
/// It handles the creation, reuse, and clearing of piece images on the chess board.
/// </summary>
public class PieceManager : Singleton<PieceManager>
{
    [SerializeField] private Sprite[] _pieceSprites; // Array of piece sprites
    [SerializeField] private Image _piecePrefab; // Prefab for the piece image

    /// <summary>
    /// Stack to pool inactive piece images for reuse.
    /// </summary>
    private Stack<Image> _piecePool = new Stack<Image>();

    /// <summary>
    /// List to keep track of currently active piece images.
    /// </summary>
    private List<Image> _activePieces = new List<Image>();

    /// <summary>
    /// Event binding for clearing the board.
    /// This event is triggered to clear all active pieces from the board.
    /// </summary>
    private EventBinding<ClearBoardEvent> _clearBoardEventBinding;

    /// <summary>
    /// Called when the script instance is being enabled.
    /// This method registers the event binding for clearing the board.
    /// </summary>
    private void OnEnable()
    {
        _clearBoardEventBinding = new EventBinding<ClearBoardEvent>(ClearActivePieces);
        EventBus<ClearBoardEvent>.Register(_clearBoardEventBinding);
    }

    /// <summary>
    /// Called when the script instance is being disabled.
    /// This method deregisters the event binding to prevent memory leaks.
    /// </summary>
    private void OnDisable()
    {
        EventBus<ClearBoardEvent>.Deregister(_clearBoardEventBinding);
    }

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// This method checks if the piece sprites and prefab are assigned correctly.
    /// </summary>
    private void Awake()
    {
        if (_pieceSprites == null || _pieceSprites.Length < 12)
        {
            Debug.LogError("Piece sprites are not assigned or insufficient in number.");
        }

        if (_piecePrefab == null)
        {
            Debug.LogError("Piece prefab is not assigned.");
        }
    }

    /// <summary>
    /// Gets a piece image based on the specified piece type and color.
    /// This method retrieves an image from the pool if available, or instantiates a new one if the pool is empty.
    /// </summary>
    /// <param name="pieceType"></param>
    /// <param name="pieceColor"></param>
    /// <param name="parent"></param>
    /// <returns></returns>
    public Image GetPieceImage(PieceType pieceType, PieceColor pieceColor)
    {
        int index = (int)pieceType + (pieceColor == PieceColor.White ? 0 : 6) - 1;

        Image pieceImage;

        // If pool has available images, pop one from the pool
        if (_piecePool.Count > 0)
        {
            pieceImage = _piecePool.Pop();
        }
        else // If pool is empty, instantiate a new piece image
        {
            pieceImage = Instantiate(_piecePrefab);
        }

        // Attach sprite into the piece image
        pieceImage.sprite = _pieceSprites[index];

        // Activate the piece image
        pieceImage.gameObject.SetActive(true);

        // Add the image into the active pieces list
        _activePieces.Add(pieceImage);

        return pieceImage;
    }

    /// <summary>
    /// Returns a piece image back to the pool.
    /// This method deactivates the image, resets its position and parent, and pushes it back to the pool for reuse.
    /// </summary>
    /// <param name="pieceImage"></param>
    public void ReturnPieceImage(Image pieceImage)
    {
        if (pieceImage != null && _activePieces.Contains(pieceImage))
        {
            // Remove the piece image from the active pieces list
            _activePieces.Remove(pieceImage);

            pieceImage.transform.SetParent(null); // Optionally reset parent
            pieceImage.rectTransform.anchoredPosition = Vector2.zero; // Reset position if needed
            pieceImage.gameObject.SetActive(false); // Deactivate the image
            _piecePool.Push(pieceImage); // Return to pool
        }
        else
        {
            Debug.LogWarning("Attempted to return a null piece image.");
        }
    }

    /// <summary>
    /// Clears all active pieces from the board.
    /// This method deactivates all active piece images, resets their parent, and pushes them back to the pool for reuse.
    /// </summary>
    private void ClearActivePieces()
    {
        foreach (var piece in _activePieces)
        {
            piece.gameObject.SetActive(false);
            _piecePool.Push(piece);
        }
        _activePieces.Clear();
    }
}
