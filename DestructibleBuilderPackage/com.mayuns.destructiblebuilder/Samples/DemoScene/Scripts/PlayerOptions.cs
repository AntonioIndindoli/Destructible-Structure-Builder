using UnityEngine;
using UnityEngine.SceneManagement; // Needed for scene reloading
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mayuns.DSB
{
    public class  PlayerOptions : MonoBehaviour
{
	public Toggle physicsVisualizationToggle;
	public Toggle FPSToggle;
	public Camera playerCamera;
	public GameObject optionsPanel;
	public Text info;
	public GameObject reloadButton; // Reference to the reload button
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
		reloadButton.SetActive(false); // Hide the reload button at the start
	}

	public void OnEscape(InputValue value)
	{
		if (isCameraLocked)
		{
			UnlockCursor();
			optionsPanel.SetActive(true); // Show the options panel
			reloadButton.SetActive(true); // Show the reload button
			info.gameObject.SetActive(false); // Hide info panel
		}
		else
		{
			LockCursor();
			optionsPanel.SetActive(false); // Hide the options panel
			reloadButton.SetActive(false); // Hide the reload button
			info.gameObject.SetActive(true); // Show the info panel again
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

	void Update()
	{
		if (isFPSCounterVisible && FPSCounterTMP != null)
		{
			accumulatedTime += Time.deltaTime;
			FPSCount++;
			if (accumulatedTime >= updateInterval)
			{
				float FPS = FPSCount / accumulatedTime;
				FPSCounterTMP.text = "" + Mathf.Ceil(FPS).ToString();
				accumulatedTime = 0f;
				FPSCount = 0;
			}
		}
	}



	// Method to reload the scene
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