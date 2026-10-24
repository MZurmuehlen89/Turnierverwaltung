using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;

namespace GuiTest.Framework;

/// <summary>Polls a condition instead of sleeping for a fixed time. UI Automation calls against elements that are
/// being rebuilt may throw; such exceptions count as "not yet" and are reported if the wait times out.</summary>
public static class Wait
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private static readonly object _fulfilled = new();

    /// <summary>Returns the first non-null result of <paramref name="probe"/>.</summary>
    /// <exception cref="WaitTimeoutException">No result within <paramref name="timeout"/>.</exception>
    public static T UntilFound<T>(Func<T?> probe, TimeSpan timeout, string description) where T : class
    {
        var elapsed = Stopwatch.StartNew();
        Exception? lastError = null;
        while (true)
        {
            var result = Probe(probe, ref lastError);
            if (result is not null) return result;
            if (elapsed.Elapsed >= timeout) throw new WaitTimeoutException(description, timeout, lastError);
            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>Waits until <paramref name="condition"/> is true.</summary>
    public static void Until(Func<bool> condition, TimeSpan timeout, string description) =>
        UntilFound(() => condition() ? _fulfilled : null, timeout, description);

    /// <summary>Waits until <paramref name="condition"/> has been true without interruption for
    /// <paramref name="stableFor"/> — for states that flicker, e.g. a busy flag between two loading steps.</summary>
    public static void UntilStable(Func<bool> condition, TimeSpan stableFor, TimeSpan timeout, string description)
    {
        var elapsed = Stopwatch.StartNew();
        Stopwatch? stable = null;
        Exception? lastError = null;
        while (true)
        {
            if (Probe(() => condition() ? _fulfilled : null, ref lastError) is not null)
            {
                stable ??= Stopwatch.StartNew();
                if (stable.Elapsed >= stableFor) return;
            }
            else
            {
                stable = null;
            }

            if (elapsed.Elapsed >= timeout) throw new WaitTimeoutException(description, timeout, lastError);
            Thread.Sleep(PollInterval);
        }
    }

    private static T? Probe<T>(Func<T?> probe, ref Exception? lastError) where T : class
    {
        try
        {
            return probe();
        }
        catch (Exception e) when (e is COMException or ElementNotAvailableException)
        {
            lastError = e;
            return null;
        }
    }
}

/// <summary>A <see cref="Wait"/> ran out of time; the message names what was awaited.</summary>
public sealed class WaitTimeoutException(string description, TimeSpan timeout, Exception? lastError)
    : TimeoutException($"Timed out after {timeout.TotalSeconds:0.#} s waiting for: {description}", lastError);
