using System;
using System.Collections.Generic;

namespace Supabase.Realtime;

/// <summary>
/// A thread-safe set of event handlers. Handlers are unique, and dispatch runs over a snapshot so
/// handlers may be added, removed or cleared (including by a handler itself) while an event is dispatched.
/// </summary>
/// <typeparam name="THandler">The handler delegate type.</typeparam>
internal sealed class HandlerCollection<THandler> where THandler : Delegate
{
    private readonly object sync = new();
    private readonly List<THandler> handlers = new();

    /// <summary>Adds a handler unless it is already registered.</summary>
    public void Add(THandler handler)
    {
        lock (this.sync)
        {
            if (!this.handlers.Contains(handler))
                this.handlers.Add(handler);
        }
    }

    /// <summary>Removes a handler; does nothing if it is not registered.</summary>
    public void Remove(THandler handler)
    {
        lock (this.sync)
            this.handlers.Remove(handler);
    }

    /// <summary>Removes every handler.</summary>
    public void Clear()
    {
        lock (this.sync)
            this.handlers.Clear();
    }

    /// <summary>Returns a copy of the current handlers, unaffected by later changes.</summary>
    public THandler[] Snapshot()
    {
        lock (this.sync)
            return this.handlers.ToArray();
    }
}
