using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BarDayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BarDayManager dayManager;

    [Header("Panel")]
    [SerializeField] private GameObject dayControlPanel;

    private FPSPlayerController interactingPlayer;

    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button StartDayButton;
    [SerializeField] private Button CloseButton;

    private void Start()
    {
        if (dayControlPanel != null)
            dayControlPanel.SetActive(false);
    }

    private void Update()
    {
        if (dayManager == null)
        {
            return;
        }

        if(dayText != null)
        {
            dayText.text = $"DAY {dayManager.CurrentDay}";
        }

        if(StartDayButton != null)
        {
            StartDayButton.interactable = dayManager.CurrentState == BarDayState.Preparation;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (dayManager == null)
        {
            return;
        }

        switch (dayManager.CurrentState)
        {
            case BarDayState.Preparation:
                if(timerText != null)
                {
                    timerText.text = "--:--";
                }

                if(stateText != null)
                {
                    stateText.text = "PREPARATION";
                }
                break;

            case BarDayState.Open:
                if(timerText != null)
                {
                    timerText.text = FormatTime(dayManager.TimeRemaining);
                }

                if(stateText != null)
                {
                    stateText.text = "BAR OPEN";
                }
                break;

            case BarDayState.Closing:
                if(timerText != null)
                {
                    timerText.text = FormatTime(dayManager.TimeRemaining);;
                }

                if(stateText != null)
                {
                    stateText.text = "CLOSING";
                }
                break;

            case BarDayState.DayEnded:
                if(timerText != null)
                {
                    timerText.text = "00:00";
                }

                if(stateText != null)
                {
                    stateText.text = "DAY ENDED";
                }
                break;
        }
    }

    public void OpenDayControlPanel(FPSPlayerController player)
    {
        if (dayControlPanel == null || player == null)
        {
            return;
        }

        interactingPlayer = player;

        dayControlPanel.SetActive(true);
        interactingPlayer.SetGameplayInputEnabled(false);
    }

    public void CloseDayControlPanel()
    {
        if (dayControlPanel == null)
        {
            return;
        }

        dayControlPanel.SetActive(false);

        if (interactingPlayer != null)
        {
            interactingPlayer.SetGameplayInputEnabled(true);
            interactingPlayer = null;
        }
    }

    public void OnStartDayButtonClicked()
    {
        if(dayManager == null)
        {
            return;
        }

        dayManager.RequestStartDay();

        CloseDayControlPanel();
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);

        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        return $"{minutes:00}:{remainingSeconds:00}";
    }
}
