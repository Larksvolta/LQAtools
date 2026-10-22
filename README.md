# LQAtools

LQAtools is a Windows localization QA tool with seven workspaces: Checklist,
Bug Tracker, Changelog, Text Files, Glossary, Tasks and Compliance. It runs either against local files or
against a shared ASP.NET Core server, so several people can work on the same
data at once.

## Requirements

- Windows 10 or 11
- .NET 10 SDK
- PowerShell 5.1 or later
- A Windows account (the server uses Windows Integrated Authentication)

Build the client once before launching it:

```powershell
dotnet build LQAtools\LQAtools.csproj -c Debug
dotnet build LQAtools.Server\LQAtools.Server.csproj -c Debug
```

## Start the server

From the repository root:

```powershell
.\scripts\run-server.ps1
```

This starts the server at `http://localhost:5217` with strict Windows
authentication: anonymous API calls are rejected with `401`. Stop it with
`Ctrl+C`.

To simulate several users on one machine, enable the development-only identity
override:

```powershell
.\scripts\run-server.ps1 -AllowUserOverride
```

The override is accepted only for loopback connections and only while the
server runs with this switch. Do not use it in production.

### Server options

| Option | Description |
| --- | --- |
| `-Url <url>` | Address to bind. Default `http://localhost:5217`. |
| `-AllowUserOverride` | Allow loopback clients to choose their user with `X-LQAtools-User` (via the client script's `-User`). |
| `-AllowedGroup <name>` | Only members of this Windows/AD group may use the server. The group must resolve at startup. |
| `-AllowInsecureRemote` | Allow plain HTTP from non-local clients. Off by default; credentials are not protected without HTTPS. |
| `-Configuration <name>` | Build configuration. Default `Debug`. |
| `-NoBuild` | Start without rebuilding the server. |

To accept LAN connections:

```powershell
.\scripts\run-server.ps1 -Url http://0.0.0.0:5217
```

Remote clients must normally use `https://`. Plain HTTP to a non-loopback
address is rejected unless the server is started with `-AllowInsecureRemote`
and the client is started with `LQATOOLS_ALLOW_INSECURE=1`; use that only on a
trusted, isolated network.

## Start clients

With the server running, open one PowerShell window per client:

```powershell
.\scripts\run-client.ps1 -Server http://localhost:5217
```

Without `-User`, the client uses the real Windows account.

### Several users on one machine

Start the server with `-AllowUserOverride`, then launch one client per user:

```powershell
.\scripts\run-client.ps1 -Server http://localhost:5217 -User Alice
.\scripts\run-client.ps1 -Server http://localhost:5217 -User Bob
```

For a one-step demo, run `.\run-demo.cmd` from the repository root. It starts
the server with the identity override and two clients as Alice and Bob.
Pass other names as arguments: `.\run-demo.cmd Carol Dave`. Port 5217 must be
free when it starts.

Each client appears to the server as a different user, so selections,
presence and edits can be tested side by side. The simulated name is set in
`LQATOOLS_USER` for the launched client process.

The `-Server` value is saved in `server.txt` next to the client executable.
Later launches can omit `-Server` and reuse it. Choose `Use Local` in the
client's Settings dialog to switch back to standalone local files.

### Client options

| Option | Description |
| --- | --- |
| `-Server <url>` | Server base URL. Writes `server.txt` next to the executable. |
| `-User <name>` | Simulated user for this client. Requires the server's `-AllowUserOverride`; otherwise the Windows account is used. |
| `-Configuration <name>` | Build configuration to launch. Default `Debug`. |
| `-Wait` | Keep the shell waiting until the client exits. |

### Real users

Omit `-User` on each machine or Windows session. The server then uses each
caller's Windows identity. If the clients are on other machines, the server
must listen on a reachable address and use HTTPS; members of the configured
`-AllowedGroup` (if any) are granted access. Simulated users created with
`-User` have no Windows group claims, so they do not satisfy `-AllowedGroup`.

## Data

- Server database: `LQAtools.Server\Storage\lqatools.db`
- Screenshot blobs: `LQAtools.Server\Storage\blobs\`
- Client server setting: `server.txt` beside the built executable

The server creates the database on first start. With the server stopped, import
the JSON/Excel data from `Data\` into it:

```powershell
dotnet run --project LQAtools.Server -- --import
```

Deleting the database resets all server-side notes, rows and markings.

## Troubleshooting

- **`401 Unauthorized`**: the client is not authenticated, or `-User` was used
  without starting the server with `-AllowUserOverride`.
- **`Client not built`**: run the client build command above.
- **Scripts are blocked**: run one with
  `powershell -ExecutionPolicy Bypass -File .\scripts\run-client.ps1 ...`.
- **Remote client gets 403/blocked**: plain HTTP to a non-loopback server is
  refused by design; use HTTPS or the explicit insecure switches on a trusted
  network.
