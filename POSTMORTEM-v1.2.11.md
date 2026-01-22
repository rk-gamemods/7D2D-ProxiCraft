# Post-Mortem: Multiplayer Container Crash

**Version:** Fixed in v1.2.11  
**Severity:** Critical (100% crash rate in multiplayer)  
**Root Cause:** Infinite recursion due to incorrect C# base method syntax

---

## The Bug

When any player opened a container (chest, mailbox, storage box, etc.) in multiplayer, all connected clients would instantly crash. The server stayed running, but every player got disconnected.

---

## Why This Was Extremely Difficult to Diagnose

### The Misleading Error Message

When clients crashed, their logs showed errors like:

```
Unknown NetPackage ID: 846
Unknown NetPackage ID: 1024
Unknown NetPackage ID: 512
```

These numbers looked like corrupted network packet IDs - suggesting the mod wasn't registering its network packets correctly. This sent us down a completely wrong debugging path.

### The Numbers Were Actually Coordinates

The "packet ID" in each error was different for every container opened:

- A mailbox at position X=846 produced `"NetPackage ID: 846"`
- A chest at position X=1024 produced `"NetPackage ID: 1024"`
- A workstation at position X=512 produced `"NetPackage ID: 512"`

When multiple users reported the bug, their error messages all had different numbers, making it seem like separate, unrelated issues. The pattern only became clear when we noticed the numbers matched container world coordinates.

### The Real Error Was Hidden

The actual crash cause - a stack overflow - was:

1. Caught by a try/catch block on the server
2. Only logged as a DEBUG message (invisible unless debug mode is enabled)
3. Buried in the server log, not the client logs where users were looking

The server log revealed the truth:

```
[ProxiCraft] [DEBUG] [Network] Failed to write lock packet: The requested operation caused a stack overflow.
```

---

## The Technical Cause

ProxiCraft broadcasts a "container lock" message when players open containers, so other players know not to pull items from it. The code to send this message had a subtle but catastrophic bug:

### The Broken Code

```csharp
public override void write(PooledBinaryWriter _bw)
{
    ((NetPackage)this).write(_bw);  // WRONG!
    // ... write our data
}
```

### What It Should Have Been

```csharp
public override void write(PooledBinaryWriter _bw)
{
    base.write(_bw);  // CORRECT
    // ... write our data
}
```

### Why This Matters

In C#, when you override a method and need to call the parent's version:

- `base.write()` correctly calls the parent class
- `((ParentClass)this).write()` **still calls the child class** due to how virtual methods work

The broken code was calling itself, creating an infinite loop that crashed after thousands of recursions.

---

## The Cascade of Failures

1. Player opens a container
2. Server tries to broadcast lock packet
3. `write()` calls itself → stack overflow (caught silently)
4. Half-written garbage data gets broadcast to all clients
5. Clients try to parse garbage as a network packet
6. Game reads random bytes as "packet ID" (which happens to be the X coordinate)
7. All clients crash with "Unknown NetPackage ID: [random number]"

---

## The Fix

One line change in [NetPackagePCLock.cs](src/Network/NetPackagePCLock.cs#L55):

```diff
- ((NetPackage)this).write(_bw);
+ base.write(_bw);
```

---

## Lessons Learned

1. **Misleading error messages can send you on a wild goose chase.** The "Unknown NetPackage ID" error seemed authoritative but was completely wrong.

2. **Variable error data can obscure patterns.** Different users reporting different packet IDs made bugs seem unrelated.

3. **Enable debug logging from the start.** The real error was only visible in server debug logs.

4. **Silent exception handling can hide critical failures.** The stack overflow was caught and logged quietly, while the cascading effects crashed clients.

5. **Check server logs, not just client logs.** The crash manifested on clients, but the cause was server-side.

6. **Virtual method dispatch in C# is strict.** Casting `this` to a base type does NOT bypass virtual dispatch - always use `base.Method()` to call parent implementations.

---

## Timeline

| Date | Event |
|------|-------|
| 2026-01-18 | Bug reported by multiple users with different "NetPackage ID" errors |
| 2026-01-19 | Initial investigation focused on packet registration (wrong path) |
| 2026-01-20 | Discovered pattern: packet IDs matched container X coordinates |
| 2026-01-20 | Enabled server debug logging, found stack overflow message |
| 2026-01-21 | Root cause identified: incorrect base method call syntax |
| 2026-01-21 | Fix included in v1.2.11 |
