using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;

/// <summary>
/// LichessPuzzleFetcher is a MonoBehaviour that fetches chess puzzles from the Lichess API.
/// It can retrieve the daily puzzle or the next puzzle in the sequence.
/// </summary>
public class LichessPuzzleFetcher : MonoBehaviour
{
    private const string url = "https://lichess.org/api/puzzle/";
    [SerializeField] private LichessPuzzleResponse lichessPuzzleResponse;

    /// <summary>
    /// Fetches the daily puzzle from the Lichess API.
    /// This method is called when the user wants to get the daily puzzle.
    /// </summary>
    public void GetDailyPuzzle()
    {
        StartCoroutine(FetchPuzzle(PuzzleCallMode.daily));
    }

    /// <summary>
    /// Fetches the next puzzle in the sequence from the Lichess API.
    /// This method is called when the user wants to get the next puzzle after solving the current one.
    /// </summary>
    public void GetNextPuzzle()
    {
        StartCoroutine(FetchPuzzle(PuzzleCallMode.next));
    }

    /// <summary>
    /// Fetches a puzzle from the Lichess API based on the specified mode (daily or next).
    /// This method uses UnityWebRequest to make an asynchronous request to the Lichess API.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    IEnumerator FetchPuzzle(PuzzleCallMode mode)
    {
        // Construct the URL based on the mode
        UnityWebRequest request = UnityWebRequest.Get($"{url}{mode}");

        // Wait for the request to complete
        yield return request.SendWebRequest();

        // Check for errors in the request
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            string json = request.downloadHandler.text;

            // Deserialize the JSON response into a LichessPuzzleResponse object
            lichessPuzzleResponse = JsonConvert.DeserializeObject<LichessPuzzleResponse>(json);

            // Start a new game with the puzzle's PGN
            GameManager.Instance.StartGame(lichessPuzzleResponse);
        }
    }
}

/// <summary>
/// PuzzleCallMode is an enumeration that defines the modes for fetching puzzles from the Lichess API.
/// It can be either 'daily' for the daily puzzle or 'next' for the next puzzle in the sequence.
/// </summary>
public enum PuzzleCallMode
{
    daily,
    next
}


[System.Serializable]
public class LichessPuzzleResponse {
    public Game game;
    public Puzzle puzzle;
}

[System.Serializable]
public class Game {
    public string id;
    public bool rated;
    public string pgn;
    public string clock;
    public Perf perf;
    public Player[] players;
}

[System.Serializable]
public class Perf {
    public string key;
    public string name;
}

[System.Serializable]
public class Player {
    public string name;
    public string id;
    public string color;
    public int rating;
}

[System.Serializable]
public class Puzzle {
    public string id;
    public int rating;
    public int plays;
    public string[] solution;
    public string[] themes;
    public int initialPly;
}

