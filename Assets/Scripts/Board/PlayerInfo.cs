using UnityEngine;
using UnityEngine.UI;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] private CanvasGroup playerInfoCanvasGroup;
    [SerializeField] private TMPro.TextMeshProUGUI playerNameText;
    [SerializeField] private TMPro.TextMeshProUGUI playerRatingText;
    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private Sprite defaultAvatar;
    [SerializeField] private Sprite defaultBotAvatar;
    [SerializeField] private GameObject crownImage;

    /// <summary>
    /// Sets the player's name, rating, and avatar.
    /// This method is used to update the player's information displayed in the UI.
    /// </summary>
    /// <param name="playerName"></param>
    /// <param name="playerRating"></param>
    /// <param name="playerAvatar"></param>
    public void SetPlayerInfo(string playerName, int playerRating, Sprite playerAvatar)
    {
        playerNameText.text = playerName;
        playerRatingText.text = $"Rating: {playerRating}";
        if (playerAvatar != null)
        {
            playerAvatarImage.sprite = playerAvatar;
        }
        else
        {
            playerAvatarImage.sprite = defaultAvatar; // Set to a default avatar if none is provided
        }
        SetCrownVisibility(false);
    }

    /// <summary>
    /// Sets the player's information for a bot player.
    /// This method is used to update the player's information displayed in the UI when the player is a bot.
    /// </summary>
    /// <param name="isBot"></param>
    public void SetPlayerInfo(bool isBot)
    {
        if (isBot)
        {
            playerNameText.text = "Bot";
            playerRatingText.text = $"Elo: {StockfishController.Instance.Elo}";
            playerAvatarImage.sprite = defaultBotAvatar; // Set to a default bot avatar if needed
        }
        else
        {
            playerNameText.text = "Player";
            playerRatingText.text = "N/A";
            playerAvatarImage.sprite = defaultAvatar; // Set to a default player avatar if needed
        }
        SetCrownVisibility(false);
    }

    public void ShowPlayerInfo()
    {
        playerInfoCanvasGroup.alpha = 1f;
        playerInfoCanvasGroup.interactable = true;
        playerInfoCanvasGroup.blocksRaycasts = true;
    }

    public void FadePlayerInfo()
    {
        playerInfoCanvasGroup.alpha = .5f;
        playerInfoCanvasGroup.interactable = false;
        playerInfoCanvasGroup.blocksRaycasts = false;
    }

    public void SetCrownVisibility(bool isVisible)
    {
        crownImage.SetActive(isVisible);
    }
}
