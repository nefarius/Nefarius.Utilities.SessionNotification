using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PInvoke;

namespace Nefarius.Utilities.SessionNotification;

/// <summary>
///     Provides an event listener for various session change events.
/// </summary>
public class SessionChangeHandler : IDisposable
{
    private const int NOTIFY_FOR_THIS_SESSION = 0;
    private const int NOTIFY_FOR_ALL_SESSIONS = 1;

    private const int WM_WTSSESSION_CHANGE = 0x2b1;

    private const int WTS_CONSOLE_CONNECT = 0x1; // A session was connected to the console terminal.
    private const int WTS_CONSOLE_DISCONNECT = 0x2; // A session was disconnected from the console terminal.
    private const int WTS_REMOTE_CONNECT = 0x3; // A session was connected to the remote terminal.
    private const int WTS_REMOTE_DISCONNECT = 0x4; // A session was disconnected from the remote terminal.
    private const int WTS_SESSION_LOGON = 0x5; // A user has logged on to the session.
    private const int WTS_SESSION_LOGOFF = 0x6; // A user has logged off the session.
    private const int WTS_SESSION_LOCK = 0x7; // A session has been locked.
    private const int WTS_SESSION_UNLOCK = 0x8; // A session has been unlocked.
    private const int WTS_SESSION_REMOTE_CONTROL = 0x9; // A session has changed its remote controlled status.

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Thread _thread;

    private readonly int _flags;

    private IntPtr _windowHandle;

    /// <summary>
    ///     Creates a new session change listener.
    /// </summary>
    /// <param name="allSessions">True to listen to all sessions, false to only listen to the current session (default).</param>
    public SessionChangeHandler(bool allSessions = false)
    {
        _flags = allSessions ? NOTIFY_FOR_ALL_SESSIONS : NOTIFY_FOR_THIS_SESSION;

        _thread = new Thread(Start)
        {
            IsBackground = true
        };

        _thread.Start();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [DllImport("Wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer, out int pBytesReturned);

    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pointer);

    [DllImport(nameof(Kernel32), SetLastError = true)]
    private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);

    [DllImport("WtsApi32.dll")]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, [MarshalAs(UnmanagedType.U4)] int dwFlags);

    [DllImport("WtsApi32.dll")]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    public event Action<int> SessionLogon;

    public event Action<int> SessionLogoff;

    public event Action<int> SessionLock;

    public event Action<int> SessionUnlock;

    /// <summary>
    ///     Resolves a username for a given session ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="prependDomain">True to prepend domain name, false to only return the username.</param>
    /// <returns>The username.</returns>
    public static string GetUsernameBySessionId(int sessionId, bool prependDomain)
    {
        IntPtr buffer;
        int strLen;
        var username = "SYSTEM";
        if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSUserName, out buffer, out strLen) &&
            strLen > 1)
        {
            username = Marshal.PtrToStringAnsi(buffer);
            WTSFreeMemory(buffer);
            if (prependDomain)
                if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSDomainName, out buffer,
                        out strLen) && strLen > 1)
                {
                    username = Marshal.PtrToStringAnsi(buffer) + "\\" + username;
                    WTSFreeMemory(buffer);
                }
        }

        return username;
    }

    private static string GenerateRandomString()
    {
        // Creating object of random class
        var rand = new Random();

        // Choosing the size of string
        // Using Next() string
        var stringlen = rand.Next(4, 10);
        int randValue;
        var sb = new StringBuilder();
        char letter;
        for (var i = 0; i < stringlen; i++)
        {
            // Generating a random number.
            randValue = rand.Next(0, 26);

            // Generating random character by converting
            // the random number into character.
            letter = Convert.ToChar(randValue + 65);

            // Appending the letter to string.
            sb.Append(letter);
        }

        return sb.ToString();
    }

    private unsafe void Start(object parameter)
    {
        var className = GenerateRandomString(); // random string to avoid conflicts
        var wndClass = User32.WNDCLASSEX.Create();

        fixed (char* cln = className)
        {
            wndClass.lpszClassName = cln;
        }

        var wndProc = new User32.WndProc(WndProc);

        wndClass.style = User32.ClassStyles.CS_HREDRAW | User32.ClassStyles.CS_VREDRAW;
        wndClass.lpfnWndProc = wndProc;
        wndClass.cbClsExtra = 0;
        wndClass.cbWndExtra = 0;
        wndClass.hInstance = GetModuleHandle(IntPtr.Zero);

        User32.RegisterClassEx(ref wndClass);

        var windowHandle = User32.CreateWindowEx(0, className, GenerateRandomString(), 0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
        _windowHandle = windowHandle;

        if (!WTSRegisterSessionNotification(_windowHandle, _flags))
            Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

        MessagePump();
    }

    private void MessagePump()
    {
        var msg = Marshal.AllocHGlobal(Marshal.SizeOf<User32.MSG>());
        int retVal;
        while ((retVal = User32.GetMessage(msg, IntPtr.Zero, 0, 0)) != 0 &&
               !_cancellationTokenSource.Token.IsCancellationRequested)
            if (retVal == -1)
            {
                break;
            }
            else
            {
                User32.TranslateMessage(msg);
                User32.DispatchMessage(msg);
            }
    }

    private unsafe IntPtr WndProc(IntPtr hwnd, User32.WindowMessage msg, void* wParam, void* lParam)
    {
        if (msg == (User32.WindowMessage)WM_WTSSESSION_CHANGE)
        {
            switch ((int)wParam)
            {
                case WTS_SESSION_LOGON:
                    SessionLogon?.Invoke((int)lParam);
                    break;
                case WTS_SESSION_LOGOFF:
                    SessionLogoff?.Invoke((int)lParam);
                    break;
                case WTS_SESSION_LOCK:
                    SessionLock?.Invoke((int)lParam);
                    break;
                case WTS_SESSION_UNLOCK:
                    SessionUnlock?.Invoke((int)lParam);
                    break;
            }

            return IntPtr.Zero;
        }

        return User32.DefWindowProc(hwnd, msg, (IntPtr)wParam, (IntPtr)lParam);
    }

    private void ReleaseUnmanagedResources()
    {
        WTSUnRegisterSessionNotification(_windowHandle);
        User32.DestroyWindow(_windowHandle);
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing) _cancellationTokenSource?.Dispose();
        _thread.Join();
    }

    ~SessionChangeHandler()
    {
        Dispose(false);
    }

    private enum WTS_INFO_CLASS
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType,
        WTSIdleTime,
        WTSLogonTime,
        WTSIncomingBytes,
        WTSOutgoingBytes,
        WTSIncomingFrames,
        WTSOutgoingFrames,
        WTSClientInfo,
        WTSSessionInfo
    }
}