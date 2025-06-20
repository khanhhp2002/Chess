using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class StockfishTcpClient : Singleton<StockfishTcpClient>
{

    [Header("Engine Settings")]
    [SerializeField] private int depth = 15; // Search depth
    [SerializeField] private int threads = 1; // Number of threads (1 - 1024)
    [SerializeField] private int hashSize = 128; // Hash table size in MB (1 - 33554432)
    [SerializeField] private bool ponder = false; // Enable pondering
    [SerializeField] private bool uci_chess960 = false; // Enable Chess960 (Fischer Random Chess)
    [SerializeField] private int elo = 1320; // ELO rating (1320 - 3190) 
    [SerializeField] private bool forceThreadCountEqualCpuCores = true; // Force thread count to match CPU cores
    [SerializeField] private GameObject LoadingCover; // Reference to the loading cover UI
    [SerializeField] private TMP_InputField hostIpInputField; // Input field for host IP
    [SerializeField] private TMP_InputField portInputField; // Input field for port number

    private int newElo;
    private int newDepth;
    private TcpClient client;
    private StreamWriter writer;
    private StreamReader reader;
    private bool isConnected = false;

    private bool isEngineReady = false;
    private bool isAnalyzing = false;

    // Events
    public event Action<string> OnBestMoveFound;
    public event Action<float> OnPositionEvaluated;
    public event Action<string> OnEngineOutput;
    public event Action OnEngineReady;
    public event Action<string> OnError;

    private CancellationTokenSource cancellationTokenSource;


    async void Connect(string ip, int port)
    {
        newElo = elo;
        newDepth = depth;

        await ConnectToServer(ip, port); // Change IP if hosted elsewhere

        if (isConnected)
        {
            LoadingCover.SetActive(false); // Hide loading cover if connected successfully
            InitializeEngine();

            // Start listening to responses
            cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => ListenToServer(cancellationTokenSource.Token));

        }
        else
        {
            LoadingCover.SetActive(true); // Show loading cover if connection failed
            Debug.LogError("Failed to connect to Stockfish TCP Server.");
        }
    }

    [ContextMenu("Connect to Stockfish Server")]
    public void SetHostAndPort()
    {
        string ip = hostIpInputField.text;
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogError("Host IP cannot be empty. Please enter a valid IP address.");
            return;
        }
        int port;
        if (!int.TryParse(portInputField.text, out port) || port < 1 || port > 65535)
        {
            Debug.LogError("Invalid port number. Please enter a valid port (1-65535).");
            return;
        }
        Connect(ip, port);
    }

    public async Task ConnectToServer(string ip, int port)
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(ip, port);

            NetworkStream stream = client.GetStream();
            writer = new StreamWriter(stream) { AutoFlush = true };
            reader = new StreamReader(stream);

            isConnected = true;
            Debug.Log("Connected to Stockfish TCP Server.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the engine with settings
    /// </summary>
    private void InitializeEngine()
    {
        SendCommand("uci");
        SendCommand("isready");
        ApplySettings();
    }

    /// <summary>
    /// Sets the ELO rating for the engine.
    /// The ELO rating must be between 1320 and 3190.
    /// </summary>
    /// <param name="elo"></param>
    public void SetElo(float elo)
    {
        newElo = (int)Mathf.Clamp(elo, 1320, 3190);
    }

    /// <summary>
    /// Sets the search depth for the engine.
    /// </summary>
    /// <param name="depth"></param>
    public void SetDepth(float depth)
    {
        newDepth = (int)Mathf.Clamp(depth, 1, 50);
    }

    /// <summary>
    /// Applies new settings if they have changed
    /// </summary>
    public void ApplyNewSettings()
    {
        if (newElo != elo || newDepth != depth)
        {
            elo = newElo;
            depth = newDepth;
            UnityEngine.Debug.Log($"Applying new settings: Elo={elo}, Depth={depth}");
            ApplySettings();
        }
    }

    /// <summary>
    /// Applies current settings to the engine
    /// </summary>
    public void ApplySettings()
    {
        if (isEngineReady)
        {
            //SendCommand($"setoption name Skill Level value {skillLevel}");
            // SendCommand($"setoption name Threads value {threads}");
            // SendCommand($"setoption name Hash value {hashSize}");
            // SendCommand($"setoption name Ponder value {(ponder ? "true" : "false")}");
            // SendCommand($"setoption name UCI_Chess960 value {(uci_chess960 ? "true" : "false")}");
            // New elo and depth settings
            SendCommand($"setoption name UCI_Elo value {elo}");
            SendCommand("ucinewgame");
        }
        else
        {
            GameManager.Instance.OpenNotificationWindow("Engine not ready", "Please wait for the engine to initialize before applying settings.");
            return;
        }
    }

    /// <summary>
    /// Sets up a position using FEN notation
    /// </summary>
    public void SetPosition(string fen)
    {
        if (!isEngineReady)
        {
            OnError?.Invoke("Engine not ready");
            return;
        }

        SendCommand($"position fen {fen}");
    }

    /// <summary>
    /// Finds the best move for the current position
    /// </summary>
    public void FindBestMove()
    {
        if (!isEngineReady)
        {
            OnError?.Invoke("Engine not ready");
            return;
        }

        if (isAnalyzing)
        {
            SendCommand("stop");
            isAnalyzing = false;
        }

        isAnalyzing = true;
        SendCommand($"go depth {depth}");
    }

    /// <summary>
    /// Sets up a position with moves from starting position
    /// </summary>
    public void SetPositionWithMoves(List<string> moves)
    {
        if (!isEngineReady)
        {
            OnError?.Invoke("Engine not ready");
            return;
        }

        string moveString = string.Join(" ", moves);
        SendCommand($"position startpos moves {moveString}");
    }

    /// <summary>
    /// Finds the best move for the current position with infinite search
    /// This will keep searching until StopFindBestMoveInfinite is called
    /// </summary>
    public void FindBestMoveInfinite()
    {
        if (!isEngineReady || isAnalyzing)
        {
            OnError?.Invoke("Engine not ready or already analyzing");
            return;
        }

        isAnalyzing = true;
        SendCommand("go infinite");
    }

    /// <summary>
    /// Stops the infinite search for the best move
    /// This should be called when you want to stop the infinite search
    /// </summary>
    public void StopFindBestMoveInfinite()
    {
        if (isAnalyzing)
        {
            SendCommand("stop");
            isAnalyzing = false;
        }
    }

    public void SendCommand(string command)
    {
        if (!isConnected || writer == null) return;

        writer.WriteLine(command);
        Debug.Log($"[Sent] {command}");
    }

    private async Task ListenToServer(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && isConnected && client.Connected)
            {
                string line = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        Debug.Log($"[Stockfish] {line}");
                        ProcessEngineOutput(line);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                isConnected = false;
                isEngineReady = false;
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    GameManager.Instance.OpenNotificationWindow("Connection Error", $"An error occurred while reading from the Stockfish server: {ex.Message}");
                });
            }
        }
    }


    private void ProcessEngineOutput(string output)
    {

        if (output.Contains("readyok"))
        {
            isEngineReady = true;
            OnEngineReady?.Invoke();
            UnityEngine.Debug.Log($"Engine: {output}");
        }
        else if (output.StartsWith("bestmove"))
        {
            string[] parts = output.Split(' ');
            if (parts.Length > 1)
            {
                string bestMove = parts[1];
                OnBestMoveFound?.Invoke(bestMove);
            }
            isAnalyzing = false;
        }
        else if (output.StartsWith("info") && output.Contains("score cp"))
        {
            string[] parts = output.Split(' ');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "score" && parts[i + 1] == "cp")
                {
                    if (int.TryParse(parts[i + 2], out int centipawns))
                    {
                        float evaluation = centipawns / 100f; // Convert to pawn units
                        OnPositionEvaluated?.Invoke(NormalizeEval(evaluation));
                    }
                }
                else if (parts[i] == "score" && parts[i + 1] == "mate")
                {
                    // Optional: handle mate evaluation
                    if (int.TryParse(parts[i + 2], out int mateIn))
                    {
                        float evaluation = mateIn > 0 ? 10f : -10f; // Treat mate as +10 or -10
                        OnPositionEvaluated?.Invoke(NormalizeEval(evaluation));
                    }
                }
            }
        }
    }

    float NormalizeEval(float eval)
    {
        // Clamp extreme values to a sensible range, say -10 to +10
        float clampedEval = Mathf.Clamp(eval, -10f, 10f);

        // Convert from pawn units to [0, 1] where 0.5 = equal
        return 0.5f + (clampedEval / 20f);
    }

    private void OnApplicationQuit()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;

        if (client != null)
        {
            client.Close();
            client = null;
            isConnected = false;
        }
    }


    public int Elo { get => elo; set => elo = Mathf.Clamp(value, 1320, 3190); }
}
