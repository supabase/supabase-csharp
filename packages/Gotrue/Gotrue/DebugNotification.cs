#region

using System;
using System.Collections.Generic;

#endregion

namespace Supabase.Gotrue;

/// <summary>
/// Manages the debug listeners for the Gotrue Client. You'll want to install a debug listener
/// to get debug information back - especially for errors from the background RefreshToken thread.
/// </summary>
[Obsolete("The debug listener is replaced by OpenTelemetry-compatible diagnostics: subscribe to the ActivitySource and Meter named \"supabase.gotrue\". This type will be removed in v8.")]
public class DebugNotification
{
    private readonly List<Action<string, Exception?>> debugListeners = new List<Action<string, Exception?>>();

    /// <summary>
    /// Add a debug listener to the Gotrue Client. This will be called with debug information
    /// </summary>
    /// <param name="listener"></param>
    public void AddDebugListener(Action<string, Exception?> listener) => this.debugListeners.Add(listener);

    /// <summary>
    /// Send a debug message to all debug listeners
    /// </summary>
    /// <param name="message"></param>
    /// <param name="e"></param>
    public void Log(string message, Exception? e = null)
    {
        foreach (var l in this.debugListeners)
        {
            l.Invoke(message, e);
        }
    }
}