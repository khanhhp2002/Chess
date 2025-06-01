using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private EventBinding<ClearBoardEvent> _clearBoardEventBinding;

    private void OnEnable()
    {
        _clearBoardEventBinding = new EventBinding<ClearBoardEvent>(ClearActivePieces);
        EventBus<ClearBoardEvent>.Register(_clearBoardEventBinding);
    }

    private void OnDisable()
    {
        EventBus<ClearBoardEvent>.Deregister(_clearBoardEventBinding);
    }

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

    public Image GetPieceImage(PieceType pieceType, PieceColor pieceColor, Transform parent = null)
    {
        int index = (int)pieceType + (pieceColor == PieceColor.White ? 0 : 6) - 1;

        Image pieceImage;
        if (_piecePool.Count > 0)
        {
            pieceImage = _piecePool.Pop();
        }
        else
        {
            pieceImage = Instantiate(_piecePrefab);
        }

        pieceImage.sprite = _pieceSprites[index];
        pieceImage.gameObject.SetActive(true);
        _activePieces.Add(pieceImage);
        // pieceImage.transform.SetParent(parent); // Set parent and maintain local scale
        // pieceImage.transform.localPosition = Vector3.zero; // Reset position to avoid positioning issues
        // pieceImage.transform.localScale = Vector3.one * 0.7f; // Reset scale to avoid scaling issues
        // pieceImage.rectTransform.offsetMax = Vector2.zero; // Reset offset to avoid layout issues
        // pieceImage.rectTransform.offsetMin = Vector2.zero; // Reset offset to avoid layout issues
        // pieceImage.rectTransform.anchorMax = Vector2.one; // Reset anchor to avoid layout issues
        // pieceImage.rectTransform.anchorMin = Vector2.zero; // Reset anchor to avoid layout issues
        return pieceImage;
    }

    public void ReturnPieceImage(Image pieceImage)
    {
        if (pieceImage != null && _activePieces.Contains(pieceImage))
        {
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
