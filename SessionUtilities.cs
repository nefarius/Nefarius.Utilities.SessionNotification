using System.Diagnostics.CodeAnalysis;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RemoteDesktop;

namespace Nefarius.Utilities.SessionNotification;

/// <summary>
///     Utility methods for session information.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class SessionUtilities
{
    /// <summary>
    ///     Resolves a username for a given session ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="prependDomain">True to prepend domain name, false to only return the username.</param>
    /// <returns>The username.</returns>
    public static unsafe string GetUsernameBySessionId(int sessionId, bool prependDomain = false)
    {
        string username = "SYSTEM";

        if (PInvoke.WTSQuerySessionInformation(HANDLE.Null, (uint)sessionId, WTS_INFO_CLASS.WTSUserName, out PWSTR buffer,
                out uint strLen) &&
            strLen > 1)
        {
            username = new string(buffer);
            PInvoke.WTSFreeMemory(buffer);

            if (prependDomain)
            {
                if (PInvoke.WTSQuerySessionInformation(HANDLE.Null, (uint)sessionId, WTS_INFO_CLASS.WTSDomainName, out buffer,
                        out strLen) && strLen > 1)
                {
                    username = new string(buffer) + "\\" + username;
                    PInvoke.WTSFreeMemory(buffer);
                }
            }
        }

        return username;
    }

    /// <summary>
    ///     Retrieves the session identifier of the console session. The console session is the session that is currently
    ///     attached to the physical console. Note that it is not necessary that Remote Desktop Services be running for this
    ///     function to succeed.
    /// </summary>
    /// <returns>
    ///     The session identifier of the session that is attached to the physical console. If there is no session
    ///     attached to the physical console, (for example, if the physical console session is in the process of being attached
    ///     or detached), this function returns 0xFFFFFFFF.
    /// </returns>
    public static uint GetActiveConsoleSessionId()
    {
        return PInvoke.WTSGetActiveConsoleSessionId();
    }
}