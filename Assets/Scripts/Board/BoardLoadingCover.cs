using TMPro;
using UnityEngine;

public class BoardLoadingCover : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject loadingSpinner;

    public void TurnOn()
    {
        titleText.text = "Loading...";
        descriptionText.text = "Please wait while the game is being set up.";
        loadingSpinner.SetActive(true);
        this.gameObject.SetActive(true);
    }

    public void TurnOff(string title = "", string description = "")
    {
        titleText.text = title;
        descriptionText.text = description;
        loadingSpinner.SetActive(false);
        Invoke(nameof(Deactive), 3f); // Deactivate after a short delay to allow the user to read the message
    }

    public void Deactive()
    {
        gameObject.SetActive(false);
    }
}
