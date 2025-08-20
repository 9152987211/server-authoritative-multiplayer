using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkTimer
{
    private float timer;
    public float minTimeBetweenTicks { get;  }
    public int currentTick { get; private set; }

    public NetworkTimer(float serverTickRate)
    {
        minTimeBetweenTicks = 1.0f / serverTickRate;
    }

    public void UpdateTimer(float deltaTime)
    {
        timer += deltaTime;
    }

    public bool ShouldTick()
    {
        if (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            currentTick++;
            return true;
        }

        return false;
    }
}
