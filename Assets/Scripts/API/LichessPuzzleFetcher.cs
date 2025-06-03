using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;

public class LichessPuzzleFetcher : MonoBehaviour
{
    private const string url = "https://lichess.org/api/puzzle/";

    public void GetDailyPuzzle()
    {
        StartCoroutine(FetchPuzzle(PuzzleCallMode.daily));
    }

    public void GetNextPuzzle()
    {
        StartCoroutine(FetchPuzzle(PuzzleCallMode.next));
    }

    IEnumerator FetchPuzzle(PuzzleCallMode mode)
    {
        UnityWebRequest request = UnityWebRequest.Get($"{url}{mode}");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            string json = request.downloadHandler.text;
            LichessPuzzleResponse response = JsonConvert.DeserializeObject<LichessPuzzleResponse>(json);
            DisplayPuzzle(response);
            GameManager.Instance.StartGame(response.game.pgn); // Start a new game with the puzzle's PGN
        }
    }

    void DisplayPuzzle(LichessPuzzleResponse response)
    {
        Debug.Log($"Puzzle ID: {response.puzzle.id}, Rating: {response.puzzle.rating}");
        Debug.Log($"Initial Ply: {response.puzzle.initialPly}");
        Debug.Log("Solution: " + string.Join(", ", response.puzzle.solution));
        Debug.Log("PGN: " + response.game.pgn);
    }
}

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

