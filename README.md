<img src="assets/NSS-128x128.png" align="right" />

# Nefarius.Utilities.SessionNotification

[![Build status](https://ci.appveyor.com/api/projects/status/bw2ojhc7lqgvoxh0/branch/master?svg=true)](https://ci.appveyor.com/project/nefarius/nefarius-utilities-sessionnotification/branch/master) ![Requirements](https://img.shields.io/badge/Requires-.NET%20Standard%202.0-blue.svg) [![Nuget](https://img.shields.io/nuget/v/Nefarius.Utilities.SessionNotification)](https://www.nuget.org/packages/Nefarius.Utilities.SessionNotification/) [![Nuget](https://img.shields.io/nuget/dt/Nefarius.Utilities.SessionNotification)](https://www.nuget.org/packages/Nefarius.Utilities.SessionNotification/)

Utility classes to get notified about session state changes.

## Examples

```csharp
using Nefarius.Utilities.SessionNotification;

// subscribe to all sessions (required when run as Windows service)
var sch = new SessionChangeHandler(true);

// session locked event (Win + L)
sch.SessionLock += id =>
{
    var username = SessionChangeHandler.GetUsernameBySessionId(id, false);
    Console.WriteLine($"Session ID {id}, user {username} locked");
};

// session unlocked event
sch.SessionUnlock += id =>
{
    var username = SessionChangeHandler.GetUsernameBySessionId(id, false);
    Console.WriteLine($"Session ID {id}, user {username} unlocked");
};
```
