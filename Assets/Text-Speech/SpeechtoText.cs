// XRDictationToTMP.cs
// Works out-of-the-box on Windows (Editor/Standalone) using UnityEngine.Windows.Speech.DictationRecognizer.
// On Android/Quest, plug in an STT provider (Meta Voice SDK/Wit.ai, Azure, Google, etc.)
// and call AppendTextFromExternalProvider(...) with recognized strings.

using System.Text;
using UnityEngine;
using TMPro;

public class XRDictationToTMP : MonoBehaviour
{
    [Header("Output")]
    [Tooltip("TextMeshPro component to receive recognized text (TMP_Text or TMP_InputField).")]
    public TMP_Text outputText;

    [Tooltip("Optional: Prefix each session with this label.")]
    public string sessionPrefix = "You: ";

    [Header("Behavior")]
    public bool startOnEnable = true;
    public bool clearOnStart = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // Windows built-in dictation
    private UnityEngine.Windows.Speech.DictationRecognizer recognizer;
#endif

    private readonly StringBuilder sb = new StringBuilder();
    private bool isListening = false;

    void Awake()
    {
        if (outputText == null)
        {
            Debug.LogWarning("[XRDictationToTMP] No TMP_Text assigned. Drag a TextMeshPro text to 'outputText'.");
        }
    }

    void OnEnable()
    {
        if (startOnEnable) StartDictation();
    }

    void OnDisable()
    {
        StopDictation();
    }

    /// <summary>Start listening / STT.</summary>
    public void StartDictation()
    {
        if (isListening) return;

        if (clearOnStart)
        {
            sb.Clear();
            SetTMP("");
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        TryStartWindowsDictation();
#else
        // Non-Windows (e.g., Android/Quest): hook your provider here.
        // Example with Meta Voice (Wit.ai):
        //  - Install Meta Voice SDK from the Asset Store / Package.
        //  - Subscribe to its transcription events and call AppendTextFromExternalProvider(text).
        Debug.Log("[XRDictationToTMP] Dictation on this platform requires an STT SDK (e.g., Meta Voice/Wit.ai, Azure, Google).");
#endif
        isListening = true;

        if (!string.IsNullOrEmpty(sessionPrefix))
            Append(sessionPrefix);
    }

    /// <summary>Stop listening / STT.</summary>
    public void StopDictation()
    {
        if (!isListening) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (recognizer != null)
        {
            try
            {
                if (recognizer.Status == UnityEngine.Windows.Speech.SpeechSystemStatus.Running)
                    recognizer.Stop();
            }
            catch { /* ignore */ }

            recognizer.DictationResult -= OnWinResult;
            recognizer.DictationHypothesis -= OnWinHypothesis;
            recognizer.DictationComplete -= OnWinComplete;
            recognizer.DictationError -= OnWinError;

            recognizer.Dispose();
            recognizer = null;
        }
#endif
        isListening = false;
    }

    /// <summary>
    /// For non-Windows or when using an external provider: call this from your provider’s transcription callback.
    /// </summary>
    public void AppendTextFromExternalProvider(string recognized)
    {
        if (string.IsNullOrEmpty(recognized)) return;
        Append(recognized.EndsWith(" ") ? recognized : recognized + " ");
    }

    private void Append(string text)
    {
        sb.Append(text);
        SetTMP(sb.ToString());
    }

    private void SetTMP(string text)
    {
        if (!outputText) return;
        outputText.text = text;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
private void TryStartWindowsDictation()
{
    try
    {
        recognizer = new UnityEngine.Windows.Speech.DictationRecognizer(
            UnityEngine.Windows.Speech.ConfidenceLevel.Medium
        );

        recognizer.DictationResult += OnWinResult;
        recognizer.DictationHypothesis += OnWinHypothesis;
        recognizer.DictationComplete += OnWinComplete;
        recognizer.DictationError += OnWinError;

        recognizer.Start();
        Debug.Log("[XRDictationToTMP] Windows DictationRecognizer started.");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[XRDictationToTMP] Failed to start Windows DictationRecognizer: {ex.Message}");
    }
}


    private void OnWinResult(string text, UnityEngine.Windows.Speech.ConfidenceLevel confidence)
    {
        // Finalized phrase
        Append(text);
        if (!text.EndsWith(". ") && !text.EndsWith("! ") && !text.EndsWith("? "))
            Append(" ");
    }

    private void OnWinHypothesis(string text)
    {
        // Live partial — show it lightly by rendering hypothesis after a newline.
        // We don't permanently append hypothesis; we show it transiently.
        // Simple approach: show sb + "\n" + text (hypo) without committing.
        if (!outputText) return;
        outputText.text = sb.ToString() + "\n" + "<alpha=#88><i>" + text + "</i></alpha>";
    }

    private void OnWinComplete(UnityEngine.Windows.Speech.DictationCompletionCause cause)
    {
        // When the engine completes naturally, you can auto-restart to keep listening.
        if (cause == UnityEngine.Windows.Speech.DictationCompletionCause.Complete)
        {
            try { recognizer.Start(); } catch { /* ignore */ }
        }
        else
        {
            Debug.LogWarning($"[XRDictationToTMP] Dictation completed: {cause}");
        }
    }

    private void OnWinError(string error, int hresult)
    {
        Debug.LogError($"[XRDictationToTMP] Dictation error: {error} (0x{hresult:X})");
    }
#endif
}
