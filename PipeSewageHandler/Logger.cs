using System;
using System.Diagnostics;

namespace HVACLoadTerminals.PipeSewageHandler;

public static class Logger
{
    public static bool IsDebugEnabled { get; set; } = true;

    public static void Log(string message)
    {
        if (IsDebugEnabled) 
            Debug.WriteLine(message);
    }

    public static void Error(string message, Exception ex = null)
    {
        Debug.WriteLine($"ERROR: {message} {ex?.Message}");
    }
}
