using System.IO;
using HuggingFace.API;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeechRecognitionTest : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button recordButton;                  // Single button
    [SerializeField] private TextMeshProUGUI statusText;           // Status/output text
    [SerializeField] private TextMeshProUGUI recordButtonLabel;    // (Optional) label on the button

    [Header("Recording")]
    [SerializeField] private int maxDurationSeconds = 10;
    [SerializeField] private int sampleRate = 44100;

    private AudioClip clip;
    private byte[] bytes;
    private bool recording;

    private void Start()
    {
        recordButton.onClick.AddListener(ToggleRecording);
        SetButtonLabel("Start");
        recordButton.interactable = true;
        statusText.color = Color.white;
        statusText.text = "Ready";
    }

    private void Update()
    {
        // Auto-stop if mic position reaches clip length (non-looping)
        if (recording && clip != null && Microphone.IsRecording(null))
        {
            int pos = Microphone.GetPosition(null);
            if (pos >= clip.samples)
            {
                StopRecording();
            }
        }
    }

    private void ToggleRecording()
    {
        if (!recording) StartRecording();
        else            StopRecording();
    }

    private void StartRecording()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            statusText.color = Color.red;
            statusText.text = "No microphone detected.";
            return;
        }

        statusText.color = Color.white;
        statusText.text = "Recording...";
        SetButtonLabel("Stop");
        recordButton.interactable = true; // keep enabled so user can stop

        clip = Microphone.Start(null, false, maxDurationSeconds, sampleRate);
        recording = true;
    }

    private void StopRecording()
    {
        if (!recording) return;

        int position = Mathf.Max(0, Microphone.GetPosition(null));
        Microphone.End(null);

        if (clip == null || position == 0)
        {
            // Nothing captured (very short tap or mic error)
            recording = false;
            statusText.color = Color.yellow;
            statusText.text = "No audio captured. Try again.";
            SetButtonLabel("Start");
            return;
        }

        // Extract recorded samples up to 'position'
        float[] samples = new float[position * clip.channels];
        clip.GetData(samples, 0);

        bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
        recording = false;

        SendRecording();
    }

    private void SendRecording()
    {
        statusText.color = Color.yellow;
        statusText.text = "Transcribing...";
        recordButton.interactable = false; // prevent new presses while sending
        SetButtonLabel("Working...");

        HuggingFaceAPI.AutomaticSpeechRecognition(bytes, response =>
        {
            statusText.color = Color.white;
            statusText.text = response;
            recordButton.interactable = true;
            SetButtonLabel("Start");
        },
        error =>
        {
            statusText.color = Color.red;
            statusText.text = error;
            recordButton.interactable = true;
            SetButtonLabel("Start");
        });
    }

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
    {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2))
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((ushort)1); // PCM
            writer.Write((ushort)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((ushort)(channels * 2));
            writer.Write((ushort)16);

            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);

            foreach (var sample in samples)
            {
                short s = (short)Mathf.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
                writer.Write(s);
            }

            return memoryStream.ToArray();
        }
    }

    private void SetButtonLabel(string label)
    {
        if (recordButtonLabel != null) recordButtonLabel.text = label;
    }
}
