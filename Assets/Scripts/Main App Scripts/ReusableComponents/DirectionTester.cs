using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DirectionTester : MonoBehaviour
{
    [Header("UI References")]
    public Button startJourneyButton;
    public Button nextDirectionButton;
    public TextMeshProUGUI currentDirectionText;

    [Header("Audio References")]
    public AudioSource audioSource;
    public AudioClip checkpointSound;
    public AudioClip destinationSound;

    [Header("TTS Settings")]
    public bool enableVoiceInstructions = true;
    public float voiceDelay = 0.5f;

    private AndroidJavaObject tts;
    private List<TestDirection> testDirections = new List<TestDirection>();
    private int currentIndex = 0;
    private bool isJourneyStarted = false;

    [System.Serializable]
    private class TestDirection
    {
        public string instruction;
        public TurnDirection turn;
        public bool isDestination;

        public TestDirection(string inst, TurnDirection t, bool isDest = false)
        {
            instruction = inst;
            turn = t;
            isDestination = isDest;
        }
    }

    void Start()
    {
        // Initialize Android TTS
        InitializeAndroidTTS();

        // Setup audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Create static test directions
        CreateTestDirections();

        // Setup button listeners
        if (startJourneyButton != null)
        {
            startJourneyButton.onClick.AddListener(OnStartJourney);
        }

        if (nextDirectionButton != null)
        {
            nextDirectionButton.onClick.AddListener(OnNextDirection);
            nextDirectionButton.interactable = false; // Disabled until journey starts
        }

        // Update UI
        UpdateUI();
    }

    private void InitializeAndroidTTS()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, null);

                Debug.Log("[TTS Tester] Android TTS initialized successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS Tester] Failed to initialize Android TTS: {e.Message}");
            }
        }
        else
        {
            Debug.Log("[TTS Tester] Not on Android platform - TTS disabled (will log to console)");
        }
    }

    private void CreateTestDirections()
    {
        testDirections.Clear();

        // Direction 1: Start
        testDirections.Add(new TestDirection(
            "Walk straight ahead for 50 meters towards the Main Building",
            TurnDirection.Straight
        ));

        // Direction 2: Turn
        testDirections.Add(new TestDirection(
            "Turn right at the fountain and continue for 30 meters",
            TurnDirection.Right
        ));

        // Direction 3: Another turn
        testDirections.Add(new TestDirection(
            "Turn left at the Science Building entrance",
            TurnDirection.Left
        ));

        // Direction 4: Continue
        testDirections.Add(new TestDirection(
            "Walk straight for 40 meters past the library",
            TurnDirection.Straight
        ));

        // Direction 5: Destination
        testDirections.Add(new TestDirection(
            "You have arrived at the Engineering Building!",
            TurnDirection.Enter,
            true // This is the destination
        ));

        Debug.Log($"[TTS Tester] Created {testDirections.Count} test directions");
    }

    private void OnStartJourney()
    {
        Debug.Log("[TTS Tester] Starting journey!");

        isJourneyStarted = true;
        currentIndex = 0;

        // Disable start button, enable next button
        if (startJourneyButton != null)
            startJourneyButton.interactable = false;

        if (nextDirectionButton != null)
            nextDirectionButton.interactable = true;

        // Show first direction
        ShowCurrentDirection();
    }

    private void OnNextDirection()
    {
        if (!isJourneyStarted || currentIndex >= testDirections.Count)
            return;

        Debug.Log($"[TTS Tester] Moving to next direction (was at {currentIndex})");

        // Move to next direction
        currentIndex++;

        // Check if we've reached the end
        if (currentIndex >= testDirections.Count)
        {
            Debug.Log("[TTS Tester] Journey complete!");
            EndJourney();
            return;
        }

        // Show the new current direction
        ShowCurrentDirection();
    }

    private void ShowCurrentDirection()
    {
        if (currentIndex >= testDirections.Count)
            return;

        TestDirection dir = testDirections[currentIndex];

        Debug.Log($"[TTS Tester] Showing direction {currentIndex + 1}/{testDirections.Count}: {dir.instruction}");

        // Update text
        if (currentDirectionText != null)
        {
            currentDirectionText.text = $"Step {currentIndex + 1}/{testDirections.Count}\n\n{dir.instruction}";
        }

        // Play sound and speak
        if (dir.isDestination)
        {
            PlayDestinationReached();
        }
        else
        {
            PlayCheckpointReached(dir.instruction);
        }
    }

    private void PlayCheckpointReached(string instruction)
    {
        Debug.Log($"[TTS Tester] Playing checkpoint sound and speaking: {instruction}");

        // Play checkpoint sound effect
        if (checkpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(checkpointSound);
        }
        else
        {
            Debug.LogWarning("[TTS Tester] Checkpoint sound or AudioSource not assigned!");
        }

        // Speak the instruction
        if (enableVoiceInstructions)
        {
            StartCoroutine(SpeakAfterDelay(instruction, voiceDelay));
        }
    }

    private void PlayDestinationReached()
    {
        Debug.Log("[TTS Tester] Playing destination sound and speaking arrival message");

        // Play destination sound effect
        if (destinationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(destinationSound);
        }
        else
        {
            Debug.LogWarning("[TTS Tester] Destination sound or AudioSource not assigned!");
        }

        // Speak arrival message
        if (enableVoiceInstructions)
        {
            string message = "You have reached your destination. Welcome to the Engineering Building!";
            StartCoroutine(SpeakAfterDelay(message, voiceDelay));
        }
    }

    private IEnumerator SpeakAfterDelay(string message, float delay)
    {
        yield return new WaitForSeconds(delay);
        Speak(message);
    }

    private void Speak(string message)
    {
        if (!enableVoiceInstructions) return;

        if (Application.platform == RuntimePlatform.Android && tts != null)
        {
            try
            {
                tts.Call<int>("speak", message, 0, null, null);
                Debug.Log($"[TTS Tester] Speaking: {message}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS Tester] Error speaking: {e.Message}");
            }
        }
        else
        {
            Debug.Log($"[TTS Tester] Would speak: {message}");
        }
    }

    private void EndJourney()
    {
        Debug.Log("[TTS Tester] Journey ended!");

        isJourneyStarted = false;

        // Disable next button
        if (nextDirectionButton != null)
            nextDirectionButton.interactable = false;

        // Update text
        if (currentDirectionText != null)
        {
            currentDirectionText.text = "Journey Complete!\n\nPress 'Start Journey' to test again.";
        }

        // Re-enable start button for testing again
        if (startJourneyButton != null)
            startJourneyButton.interactable = true;
    }

    private void UpdateUI()
    {
        if (!isJourneyStarted)
        {
            if (currentDirectionText != null)
            {
                currentDirectionText.text = "Press 'Start Journey' to begin testing\n\n" +
                    "This will test:\n" +
                    "• Checkpoint sound effects\n" +
                    "• Destination sound effect\n" +
                    "• Text-to-speech for each direction\n" +
                    "• Turn icons display";
            }
        }
    }

    void OnDestroy()
    {
        // Shutdown TTS
        if (Application.platform == RuntimePlatform.Android && tts != null)
        {
            try
            {
                tts.Call("shutdown");
                Debug.Log("[TTS Tester] Android TTS shutdown");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS Tester] Error shutting down: {e.Message}");
            }
        }
    }
}
