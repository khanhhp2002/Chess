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
    [SerializeField] private GameObject LoadingCover; // Reference to the loading cover UI

    private int newElo;
    private int newDepth;

    private Process stockfishProcess;
    private AndroidJavaObject androidProcess;
    private StreamWriter engineInput;
    private StreamReader engineOutput;
    private AndroidJavaObject engineInputStream; // Only for Android
    private AndroidJavaObject engineOutputStream; // Only for Android
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

        newElo = elo;
        newDepth = depth;

        OnEngineReady += () =>
        {
            UnityEngine.Debug.Log("Stockfish engine is ready.");
            LoadingCover?.SetActive(false); // Hide loading cover when engine is ready
        };
        
        StartEngine();
    }

    /// <summary>
    /// Cleans up the engine process when the object is destroyed.
    /// This ensures that the engine is properly stopped and resources are released.
    /// </summary>
    private void OnDestroy()
    {
        StopEngine();
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
    /// Starts the Stockfish engine
    /// </summary>
    public void StartEngine()
    {
        try
        {
            string enginePath = PrepareAndroidStockfishExecutable();

            if (!File.Exists(enginePath))
            {
                OnError?.Invoke($"Stockfish executable not found at: {enginePath}");
                return;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            // Use JNI to launch Stockfish on Android
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                AndroidJavaObject processBuilder = new AndroidJavaObject("java.lang.ProcessBuilder", new string[] { enginePath });
                processBuilder.Call<AndroidJavaObject>("redirectErrorStream", true);

                // Now start and assign to androidProcess
                androidProcess = processBuilder.Call<AndroidJavaObject>("start");

                engineInputStream = androidProcess.Call<AndroidJavaObject>("getOutputStream");
                engineOutputStream = androidProcess.Call<AndroidJavaObject>("getInputStream");

                Debug.Log("Stockfish started using ProcessBuilder (Android)");
            }

            #else
            // PC or Editor
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

            readingOutput = true;
            engineThread = new Thread(ReadEngineOutputLoop);
            engineThread.Start();
            #endif

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
            engineThread.Join(100);
            engineThread = null;
        }

    #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (engineInputStream != null)
            {
                engineInputStream.Call("close");
                engineInputStream = null;
            }

            if (engineOutputStream != null)
            {
                engineOutputStream.Call("close");
                engineOutputStream = null;
            }

            // Optionally destroy the process
            if (androidProcess != null)
            {
                androidProcess.Call("destroy");
                androidProcess = null;
            }

            Debug.Log("[Android] Stockfish engine stopped and cleaned up.");
        }
        catch (Exception e)
        {
            OnError?.Invoke($"[Android] Error stopping engine: {e.Message}");
        }
    #else
        try
        {
            if (engineInput != null)
            {
                engineInput.Close();
                engineInput = null;
            }

            if (engineOutput != null)
            {
                engineOutput.Close();
                engineOutput = null;
            }

            if (stockfishProcess != null && !stockfishProcess.HasExited)
            {
                stockfishProcess.Kill();
                stockfishProcess.Dispose();
                stockfishProcess = null;
            }

            UnityEngine.Debug.Log("[PC] Stockfish engine stopped and cleaned up.");
        }
        catch (Exception e)
        {
            OnError?.Invoke($"[PC] Error stopping engine: {e.Message}");
        }
    #endif
    }


    private string PrepareAndroidStockfishExecutable()
    {
    #if UNITY_ANDROID && !UNITY_EDITOR
        string dstPath = Path.Combine(Application.persistentDataPath, "stockfish");

        // Only copy if not exists
        if (!File.Exists(dstPath))
        {
            string srcPath = Path.Combine(Application.streamingAssetsPath, "stockfish-android-armv8");

            // Load binary from StreamingAssets
            UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(srcPath);
            www.SendWebRequest();
            while (!www.isDone) { }

            if (string.IsNullOrEmpty(www.error))
            {
                File.WriteAllBytes(dstPath, www.downloadHandler.data);

                // Set executable permission (chmod 744)
                try
                {
                    using (var process = new AndroidJavaObject("java.lang.ProcessBuilder", new string[] { "chmod", "744", dstPath }))
                    {
                        process.Call<AndroidJavaObject>("start");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("chmod failed: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Failed to copy Stockfish: " + www.error);
            }
        }

        return dstPath;
    #else
        return GetStockfishPath();
    #endif
    }

    /// <summary>
    /// Gets the path to the Stockfish executable
    /// </summary>
    private string GetStockfishPath()
    {
        string resourcesPath = Application.streamingAssetsPath;

        return Path.Combine(resourcesPath, "stockfish-windows-x86-64-avx2.exe");
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
        if (!isEngineReady)
            return;
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (engineInputStream != null)
            {
                // Convert string to byte[] and write to OutputStream
                byte[] commandBytes = System.Text.Encoding.UTF8.GetBytes(command + "\n");

                using (AndroidJavaClass byteArrayOutputStreamClass = new AndroidJavaClass("java.io.ByteArrayOutputStream"))
                using (AndroidJavaObject byteArrayOutputStream = new AndroidJavaObject("java.io.ByteArrayOutputStream"))
                {
                    byteArrayOutputStream.Call("write", commandBytes);
                    byte[] byteArray = byteArrayOutputStream.Call<byte[]>("toByteArray");

                    engineInputStream.Call("write", byteArray);
                    engineInputStream.Call("flush");

                    Debug.Log($"[Android] Sent: {command}");
                }
            }
#else
            if (engineInput != null && stockfishProcess != null && !stockfishProcess.HasExited)
            {
                engineInput.WriteLine(command);
                engineInput.Flush();
                UnityEngine.Debug.Log($"[PC] Sent: {command}");
            }
#endif
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
            while (readingOutput)
            {
    #if UNITY_ANDROID && !UNITY_EDITOR
                if (engineOutputStream != null)
                {
                    // Check how many bytes are available to read
                    int available = engineOutputStream.Call<int>("available");

                    if (available > 0)
                    {
                        byte[] buffer = new byte[available];
                        AndroidJavaObject byteBuffer = new AndroidJavaObject("java.nio.ByteBuffer", buffer);
                        int bytesRead = engineOutputStream.Call<int>("read", buffer);

                        if (bytesRead > 0)
                        {
                            string output = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            string[] lines = output.Split('\n'); // Might read multiple lines at once

                            foreach (var line in lines)
                            {
                                string trimmed = line.Trim();
                                if (!string.IsNullOrEmpty(trimmed))
                                {
                                    string capturedLine = trimmed;
                                    UnityMainThreadDispatcher.Enqueue(() =>
                                    {
                                        ProcessEngineOutput(capturedLine);
                                        OnEngineOutput?.Invoke(capturedLine);
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
    #else
                // PC / Editor
                if (engineOutput != null && stockfishProcess != null && !stockfishProcess.HasExited && !engineOutput.EndOfStream)
                {
                    string line = engineOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        string capturedLine = line;
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            ProcessEngineOutput(capturedLine);
                            OnEngineOutput?.Invoke(capturedLine);
                        });
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
    #endif
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
    public int Elo { get => elo; set => elo = Mathf.Clamp(value, 1320, 3190); }
}