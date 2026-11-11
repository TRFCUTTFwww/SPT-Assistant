# Chat verification

Run the platform TLS and broker tests on Windows with TLS 1.3 support:

```powershell
dotnet run --project chat-tests/Tests.csproj -c Release
```

The optional live integration test requires the matching local SPT server,
the new GearMail component, Windows curl, and an existing local profile ID:

```powershell
dotnet run --project chat-tests/Tests.csproj -c Release -- --http <profile-id>
```

It creates two temporary relay identities, verifies them, exchanges encrypted
messages both ways, and leaves. It does not change the save. The HTTP test's
frame format is specific to EFT 48558. Only the outer localhost HTTPS test
ignores the self-signed server certificate; inner TLS 1.3 requires both exact
peer fingerprints and encrypted connection contexts.

These tests do not validate Unity UI, IME, game input interception or the
in-raid effects of native commands. Those checks remain manual.
