using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public enum BarDayState
{
    Preparation,
    Open,
    Closing,
    DayEnded
}


public class BarDayManager : NetworkBehaviour
{
    [Header("Customer Spawner")]
    [SerializeField] private CustomerSpawner customerSpawner;

    [Header("Day Settings")]
    [SerializeField, Min(1)]
    private int startingDay = 1;

    [SerializeField, Min(1f)]
    private float serviceDuration = 1200f;

    [SerializeField, Min(1f)]
    private float closingDuration = 30f;

    private NetworkVariable<int> currentDay = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<BarDayState> currentState = new(BarDayState.Preparation, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<double> phaseEndTime = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Coroutine dayCoroutine;
    public int CurrentDay => currentDay.Value;
    public BarDayState CurrentState => currentState.Value;

    /// <summary>
    /// Checks how much time is remaining.
    /// </summary>
    public float TimeRemaining
    {
        get
        {
            if(!IsSpawned || NetworkManager == null)
            {
                return 0f;
            }

            if(CurrentState != BarDayState.Open && CurrentState != BarDayState.Closing)
            {
                return 0f;
            }

            return Mathf.Max(0f, (float)(phaseEndTime.Value - NetworkManager.ServerTime.Time));
        }
    }

    /// <summary>
    /// Makes sure tha values to start the day are correct.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        currentDay.Value = startingDay;
        currentState.Value = BarDayState.Preparation;
        phaseEndTime.Value = 0;

        if(customerSpawner != null)
        {
            customerSpawner.SetSpawningEnabled(false);
        }
    }

    /// <summary>
    /// Request to start the day for the Host and Client.
    /// </summary>
    public void RequestStartDay()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            StartDay();
        }
        else
        {
            RequestStartDayRpc();
        }
    }

    /// <summary>
    /// Request to start the day through a call to the server for the Client.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestStartDayRpc()
    {
        StartDay();
    }

    /// <summary>
    /// Start the day.
    /// </summary>
    private void StartDay()
    {
        if(!IsServer || currentState.Value != BarDayState.Preparation)
        {
            return;
        }

        if(dayCoroutine != null)
        {
            StopCoroutine(dayCoroutine);
        }

        currentState.Value = BarDayState.Open;

        phaseEndTime.Value = NetworkManager.ServerTime.Time + serviceDuration;

        if(customerSpawner != null)
        {
            customerSpawner.SetSpawningEnabled(true);
        }

        dayCoroutine = StartCoroutine(RunDay());
    }

    /// <summary>
    /// Process of the whole day with waiting for it to end and closing and ending day mechanisms.
    /// </summary>
    private IEnumerator RunDay()
    {
        //Wait for day to end
        while(NetworkManager.ServerTime.Time < phaseEndTime.Value)
        {
            yield return null;
        }

        if (!IsServer)
        {
            yield break;
        }

        currentState.Value = BarDayState.Closing;

        if(customerSpawner != null)
        {
            customerSpawner.SetSpawningEnabled(false);
        }

        phaseEndTime.Value = NetworkManager.ServerTime.Time + closingDuration;

        // Wait for closing to end.
        while(NetworkManager.ServerTime.Time < phaseEndTime.Value)
        {
            yield return null;
        }

        if (!IsServer)
        {
            yield break;
        }

        currentState.Value = BarDayState.DayEnded;
        phaseEndTime.Value = 0;

        Debug.Log($"[BarDayManager] Day ended. Waiting for remaining customers...", this);

        while(customerSpawner != null && !customerSpawner.AreAllCustomersGone)
        {
            yield return null;
        }

        currentDay.Value++;
        currentState.Value = BarDayState.Preparation;

        Debug.Log($"[BarDayManager] Preparing Day {currentDay.Value}.", this);

        dayCoroutine = null;
    }
}
