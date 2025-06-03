using TMPro;
using UnityEngine;

/// <summary>
/// IndicatorText is a MonoBehaviour that manages the text displayed in the chess board indicators.
/// It allows setting the text and changing its color based on the cell's state (even/odd, selected).
/// </summary>
public class IndicatorText : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    public void SetText(string text)
    {
        if (_text != null)
        {
            _text.text = text;
        }
        else
        {
            Debug.LogWarning("Text component is not assigned in the Inspector.");
        }
    }

    public void SetTextColor(bool isEven, bool isSelected = false)
    {
        if (_text != null)
        {
            if (isSelected)
                _text.color = ConstantData.CELL_LIGHT_COLOR_SELECT;
            else
                _text.color = isEven ? ConstantData.CELL_DARK_COLOR : ConstantData.CELL_LIGHT_COLOR;
        }
        else
        {
            Debug.LogWarning("Text component is not assigned in the Inspector.");
        }
    }
}