using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Nefarius.Utilities.SessionNotification;

/// <summary>
///     Provides an event listener for various session change events.
/// </summary>
[SuppressMessage("ReSharper", "EventNeverSubscribedTo.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class SessionChangeHandler : IDisposable
{
	private readonly CancellationTokenSource _cancellationTokenSource = new();

	private readonly uint _flags;

	private readonly Thread _thread;

	private HWND _windowHandle;

	/// <summary>
	///     Creates a new session change listener.
	/// </summary>
	/// <param name="allSessions">True to listen to all sessions, false to only listen to the current session (default).</param>
	public SessionChangeHandler(bool allSessions = false)
	{
		_flags = allSessions ? PInvoke.NOTIFY_FOR_ALL_SESSIONS : PInvoke.NOTIFY_FOR_THIS_SESSION;

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

	/// <summary>
	///     A session was connected to the console terminal.
	/// </summary>
	public event Action<int> ConsoleConnect;

	/// <summary>
	///     A session was disconnected from the console terminal.
	/// </summary>
	public event Action<int> ConsoleDisconnect;

	/// <summary>
	///     A session was connected to the remote terminal.
	/// </summary>
	public event Action<int> RemoteConnect;

	/// <summary>
	///     A session was disconnected from the remote terminal.
	/// </summary>
	public event Action<int> RemoteDisconnect;

	/// <summary>
	///     A user has logged on to the session.
	/// </summary>
	public event Action<int> SessionLogon;

	/// <summary>
	///     A user has logged off the session.
	/// </summary>
	public event Action<int> SessionLogoff;

	/// <summary>
	///     A session has been locked.
	/// </summary>
	public event Action<int> SessionLock;

	/// <summary>
	///     A session has been unlocked.
	/// </summary>
	public event Action<int> SessionUnlock;

	/// <summary>
	///     A session has changed its remote controlled status.
	/// </summary>
	public event Action<int> SessionRemoteControl;

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
		var windowName = GenerateRandomString();
		using var hInst = PInvoke.GetModuleHandle((string)null);

		var windowClass = new WNDCLASSEXW
		{
			cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
			style = WNDCLASS_STYLES.CS_HREDRAW | WNDCLASS_STYLES.CS_VREDRAW,
			cbClsExtra = 0,
			cbWndExtra = 0,
			hInstance = (HINSTANCE)hInst.DangerousGetHandle(),
			lpfnWndProc = WndProc
		};

		fixed (char* pClassName = className)
		fixed (char* pWindowName = windowName)
		{
			windowClass.lpszClassName = pClassName;

			PInvoke.RegisterClassEx(windowClass);

			_windowHandle = PInvoke.CreateWindowEx(
				0,
				pClassName,
				pWindowName,
				0,
				0, 0, 0, 0,
				HWND.Null,
				HMENU.Null,
				new HINSTANCE(hInst.DangerousGetHandle())
			);
		}

		if (!PInvoke.WTSRegisterSessionNotification(_windowHandle, _flags))
			Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

		MessagePump();
	}

	private void MessagePump()
	{
		int retVal;
		while ((retVal = PInvoke.GetMessage(out var msg, HWND.Null, 0, 0)) != 0 &&
		       !_cancellationTokenSource.Token.IsCancellationRequested)
			if (retVal == -1)
			{
				break;
			}
			else
			{
				PInvoke.TranslateMessage(msg);
				PInvoke.DispatchMessage(msg);
			}
	}

	private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
	{
		if (msg == PInvoke.WM_WTSSESSION_CHANGE)
		{
			switch (wParam.Value)
			{
				case PInvoke.WTS_CONSOLE_CONNECT:
					ConsoleConnect?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_CONSOLE_DISCONNECT:
					ConsoleDisconnect?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_REMOTE_CONNECT:
					RemoteConnect?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_REMOTE_DISCONNECT:
					RemoteDisconnect?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_SESSION_LOGON:
					SessionLogon?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_SESSION_LOGOFF:
					SessionLogoff?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_SESSION_LOCK:
					SessionLock?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_SESSION_UNLOCK:
					SessionUnlock?.Invoke((int)lParam.Value);
					break;
				case PInvoke.WTS_SESSION_REMOTE_CONTROL:
					SessionRemoteControl?.Invoke((int)lParam.Value);
					break;
			}

			return new LRESULT(0);
		}

		return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
	}

	private void ReleaseUnmanagedResources()
	{
		PInvoke.WTSUnRegisterSessionNotification(_windowHandle);
		PInvoke.DestroyWindow(_windowHandle);
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
}