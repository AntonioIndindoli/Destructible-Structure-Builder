using Mayuns.DSB;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerOptions : MonoBehaviour
{
    public Toggle physicsVisualizationToggle;
    public Toggle FPSToggle;
    public Camera playerCamera;
    public GameObject optionsPanel;
    public Text info;
    public GameObject reloadButton;
    public float updateInterval = 0.5f;
    private float accumulatedTime = 0f;
    private int FPSCount = 0;
    public Text FPSCounterTMP;
    private bool isCameraLocked = true;
    private bool isFPSCounterVisible = true;

    void Start()
    {
        // Lock the cursor at the start
        LockCursor();

        // Set up the toggle buttons' initial state
        physicsVisualizationToggle.isOn = false;
        FPSToggle.isOn = true;

        // Set listeners for the toggles
        physicsVisualizationToggle.onValueChanged.AddListener(TogglePhysicsVisualization);
        FPSToggle.onValueChanged.AddListener(ToggleFPSCounter);

        info.gameObject.SetActive(true);

        // Hide options panel and reload button at start
        optionsPanel.SetActive(false);
        reloadButton.SetActive(false);
    }

    void Update()
    {
        // Handle Escape key toggle for UI
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isCameraLocked)
            {
                UnlockCursor();
                optionsPanel.SetActive(true);
                reloadButton.SetActive(true);
                info.gameObject.SetActive(false);
            }
            else
            {
                LockCursor();
                optionsPanel.SetActive(false);
                reloadButton.SetActive(false);
                info.gameObject.SetActive(true);
            }
        }

        // FPS counter logic
        if (isFPSCounterVisible && FPSCounterTMP != null)
        {
            accumulatedTime += Time.deltaTime;
            FPSCount++;
            if (accumulatedTime >= updateInterval)
            {
                float FPS = FPSCount / accumulatedTime;
                FPSCounterTMP.text = Mathf.Ceil(FPS).ToString();
                accumulatedTime = 0f;
                FPSCount = 0;
            }
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isCameraLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isCameraLocked = false;
    }

    private void TogglePhysicsVisualization(bool isEnabled)
    {
        StructuralStressVisualizer.SetVisualizeGizmos(isEnabled);
    }

    private void ToggleFPSCounter(bool isEnabled)
    {
        isFPSCounterVisible = isEnabled;

        if (FPSCounterTMP != null)
        {
            FPSCounterTMP.gameObject.SetActive(isEnabled);
        }
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitScene()
    {
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
