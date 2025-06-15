using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;


public class StockfishController : Singleton<StockfishController>
{
    [Header("Engine Settings")]
    [SerializeField] private int depth = 15; // Search depth
    [SerializeField] private int threads = 1; // Number of threads (1 - 1024)
    [SerializeField] private int hashSize = 128; // Hash table size in MB (1 - 33554432)
    [SerializeField] private bool ponder = false; // Enable pondering
    [SerializeField] private bool uci_chess960 = false; // Enable Chess960 (Fischer Random Chess)
    [SerializeField] private int elo = 1320; // ELO rating (1320 - 3190) 
    [SerializeField] private bool forceThreadCountEqualCpuCores = true; // Force thread count to match CPU cores
    //[SerializeField] private int skillLevel = 20; // 0-20, where 20 is strongest

    private Process stockfishProcess;
    private StreamWriter engineInput;
    private StreamReader engineOutput;
    private bool isEngineReady = false;
    private bool isAnalyzing = false;

    private Thread engineThread;
    private volatile bool readingOutput = false;

    // Events
    public event Action<string> OnBestMoveFound;
    public event Action<float> OnPositionEvaluated;
    public event Action<string> OnEngineOutput;
    public event Action OnEngineReady;
    public event Action<string> OnError;

    [System.Serializable]
    public class EngineSettings
    {
        public int skillLevel = 20;
        public int depth = 15;
        public int moveTime = 1000;
        public int threads = 1;
        public int hashSize = 128;
        public bool useBook = true;
        public bool ponder = false;
    }

    private void Start()
    {
        int logicalCores = Environment.ProcessorCount;
        UnityEngine.Debug.Log($"Detected {logicalCores} logical cores on this machine.");
        if (forceThreadCountEqualCpuCores)
        {
            threads = logicalCores;
            UnityEngine.Debug.Log($"Forcing thread count to match CPU cores: {threads}");
        }

        OnPositionEvaluated += (evaluation) =>
        {

        };
        
        StartEngine();
    }

    private void OnDestroy()
    {
        StopEngine();
    }

    /// <summary>
    /// Starts the Stockfish engine
    /// </summary>
    public void StartEngine()
    {
        try
        {
            string enginePath = GetStockfishPath();

            if (!File.Exists(enginePath))
            {
                OnError?.Invoke($"Stockfish executable not found at: {enginePath}");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            stockfishProcess = Process.Start(startInfo);
            engineInput = stockfishProcess.StandardInput;
            engineOutput = stockfishProcess.StandardOutput;

            // Start reading output in a coroutine
            readingOutput = true;
            engineThread = new Thread(ReadEngineOutputLoop);
            engineThread.Start();


            // Initialize engine
            InitializeEngine();

            UnityEngine.Debug.Log("Stockfish engine started successfully");
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Failed to start Stockfish: {e.Message}");
        }
    }

    /// <summary>
    /// Stops the Stockfish engine
    /// </summary>
    public void StopEngine()
    {
        readingOutput = false;

        if (engineThread != null && engineThread.IsAlive)
        {
            engineThread.Join(100); // Wait for thread to finish
            engineThread = null;
        }
    }

    /// <summary>
    /// Gets the path to the Stockfish executable
    /// </summary>
    private string GetStockfishPath()
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        return Path.Combine(resourcesPath, "stockfish-windows-x86-64-avx2.exe");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return Path.Combine(resourcesPath, "stockfish-windows-x86-64-avx2");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return Path.Combine(resourcesPath, "stockfish-windows-x86-64-avx2");
#else
            return Path.Combine(resourcesPath, "stockfish-windows-x86-64-avx2");
#endif
    }

    /// <summary>
    /// Initializes the engine with settings
    /// </summary>
    private void InitializeEngine()
    {
        SendCommand("uci");
        ApplySettings();
        SendCommand("isready");
    }

    /// <summary>
    /// Applies current settings to the engine
    /// </summary>
    public void ApplySettings()
    {
        if (!isEngineReady && stockfishProcess != null)
        {
            //SendCommand($"setoption name Skill Level value {skillLevel}");
            SendCommand($"setoption name Threads value {threads}");
            SendCommand($"setoption name Hash value {hashSize}");
            SendCommand($"setoption name Ponder value {(ponder ? "true" : "false")}");
            SendCommand($"setoption name UCI_Chess960 value {(uci_chess960 ? "true" : "false")}");
            SendCommand($"setoption name UCI_Elo value {elo}");
            SendCommand("ucinewgame");
        }
    }

    /// <summary>
    /// Updates engine settings
    /// </summary>
    // public void UpdateSettings(EngineSettings settings)
    // {
    //     skillLevel = settings.skillLevel;
    //     depth = settings.depth;
    //     moveTime = settings.moveTime;
    //     threads = settings.threads;
    //     hashSize = settings.hashSize;

    //     ApplySettings();
    // }

    /// <summary>
    /// Sends a command to the engine
    /// </summary>
    private void SendCommand(string command)
    {
        try
        {
            if (engineInput != null && stockfishProcess != null && !stockfishProcess.HasExited)
            {
                engineInput.WriteLine(command);
                engineInput.Flush();
                UnityEngine.Debug.Log($"Sent: {command}");
            }
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Error sending command: {e.Message}");
        }
    }

    /// <summary>
    /// Reads engine output continuously
    /// </summary>
    private void ReadEngineOutputLoop()
    {
        try
        {
            while (readingOutput && stockfishProcess != null && !stockfishProcess.HasExited)
            {
                if (engineOutput != null && !engineOutput.EndOfStream)
                {
                    string line = engineOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        // Capture local copy for thread-safety
                        string output = line;

                        // Dispatch to Unity main thread
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            ProcessEngineOutput(output);
                            OnEngineOutput?.Invoke(output);
                        });
                    }
                }
                else
                {
                    Thread.Sleep(10); // Avoid tight loop when idle
                }
            }
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Error reading engine output: {e.Message}");
        }
    }

    /// <summary>
    /// Processes engine output and triggers appropriate events
    /// </summary>
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
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnBestMoveFound?.Invoke(bestMove);
                });
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
        // else if (output.StartsWith("info depth"))
        // {
        //     // Optional: handle depth information
        //     UnityEngine.Debug.Log($"Engine Depth Info: {output}");
        // }
        // else if (output.StartsWith("option name"))
        // {
        //     // Optional: handle option settings
        //     UnityEngine.Debug.Log($"Engine Option: {output}");
        // }
        // else
        // {
        //     UnityEngine.Debug.Log($"Engine Output: {output}");
        // }
    }

    float NormalizeEval(float eval)
{
    // Clamp extreme values to a sensible range, say -10 to +10
    float clampedEval = Mathf.Clamp(eval, -10f, 10f);

    // Convert from pawn units to [0, 1] where 0.5 = equal
    return 0.5f + (clampedEval / 20f);
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

    public void StopFindBestMoveInfinite()
    {
        if (isAnalyzing)
        {
            SendCommand("stop");
            isAnalyzing = false;
        }
    }

    /// <summary>
    /// Finds the best move with time limit
    /// </summary>
    // public void FindBestMoveWithTime(int timeMs = -1)
    // {
    //     if (!isEngineReady || isAnalyzing)
    //     {
    //         OnError?.Invoke("Engine not ready or already analyzing");
    //         return;
    //     }

    //     int searchTime = timeMs > 0 ? timeMs : moveTime;
    //     isAnalyzing = true;
    //     SendCommand($"go movetime {searchTime}");
    // }

    /// <summary>
    /// Evaluates the current position
    /// </summary>
    public void EvaluatePosition()
    {
        if (!isEngineReady)
        {
            OnError?.Invoke("Engine not ready");
            return;
        }

        SendCommand($"go depth {Math.Min(depth, 10)}");
    }

    /// <summary>
    /// Gets multiple best moves (MultiPV)
    /// </summary>
    public void GetMultipleBestMoves(int numMoves)
    {
        if (!isEngineReady || isAnalyzing)
        {
            OnError?.Invoke("Engine not ready or already analyzing");
            return;
        }

        SendCommand($"setoption name MultiPV value {numMoves}");
        isAnalyzing = true;
        SendCommand($"go depth {depth}");
    }

    /// <summary>
    /// Checks if a move is legal
    /// </summary>
    public void ValidateMove(string fen, string move, System.Action<bool> callback)
    {
        StartCoroutine(ValidateMoveCoroutine(fen, move, callback));
    }

    private IEnumerator ValidateMoveCoroutine(string fen, string move, System.Action<bool> callback)
    {
        SetPosition(fen);
        yield return new WaitForSeconds(0.1f);

        // Try to make the move and see if engine accepts it
        SendCommand($"position fen {fen} moves {move}");

        bool moveAccepted = true; // In a real implementation, you'd check engine response
        callback?.Invoke(moveAccepted);
    }

    /// <summary>
    /// Stops current analysis
    /// </summary>
    public void StopAnalysis()
    {
        if (isAnalyzing)
        {
            SendCommand("stop");
            isAnalyzing = false;
        }
    }

    /// <summary>
    /// Resets the engine to starting position
    /// </summary>
    public void ResetToStartingPosition()
    {
        if (!isEngineReady)
        {
            OnError?.Invoke("Engine not ready");
            return;
        }

        SendCommand("position startpos");
        SendCommand("ucinewgame");
    }

    /// <summary>
    /// Gets engine information
    /// </summary>
    public void GetEngineInfo()
    {
        if (stockfishProcess != null && !stockfishProcess.HasExited)
        {
            SendCommand("uci");
        }
    }

    // Public properties
    public bool IsEngineReady => isEngineReady;
    public bool IsAnalyzing => isAnalyzing;
    //public int SkillLevel { get => skillLevel; set => skillLevel = Mathf.Clamp(value, 0, 20); }
    public int Depth { get => depth; set => depth = Mathf.Clamp(value, 1, 50); }
    //public int MoveTime { get => moveTime; set => moveTime = Mathf.Max(value, 100); }
}