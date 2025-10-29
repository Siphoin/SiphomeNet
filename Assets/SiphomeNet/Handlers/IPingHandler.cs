using System;

public interface IPingHandler
{
    event Action<int> OnPingChanged;
    int GetCurrentPing();
}