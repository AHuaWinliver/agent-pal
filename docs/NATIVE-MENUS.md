# Native Windows 11 folder tasks

[简体中文](NATIVE-MENUS.zh-CN.md) · [Command configuration](QUICK-TASKS.md)

The native extension adds **Send Agent Pal task** to Windows 11's main context menu, with an explicit application icon. With one enabled companion binding, selecting it opens that task directly. With multiple bindings, it opens a flyout listing the bound companions and commands. With no enabled bindings, it is hidden. Right-click a folder, its background or the desktop to use it.

## Enable

1. Install a build that includes the `shell` component.
2. Open **Default settings → General → Manage folder quick tasks…**.
3. Select **Show in the Windows 11 main menu**, choose menu locations and companion bindings, then save.
4. Complete any required system approval. The app registers the identity package and checks the native command's title, icon and enabled state before reporting success.

If registration fails, click the error status for the full Windows error. Saved definitions and previous entries are restored when command registration fails. The app does not report success just because a JSON definition was saved.

The preview uses a dedicated code-signing certificate. On the test system, package deployment rejected trust in the current-user store; enabling it requires approval to trust this public certificate in **Local Computer → Trusted People**. The installer and settings do not import it silently. The private key is non-exportable and is not included in the package or source. No Root certificate or Developer Mode change is required. Production distributions should use a production signing identity trusted by their target systems.

Windows 10 and source builds without a signed identity package can use the separately selectable compatibility menu. On Windows 11, compatibility entries appear under **Show more options**.

## Implementation

- `src/DesktopTasks/AgentPal.Shell/AgentPalShell.cpp` implements native `IExplorerCommand` and `IObjectWithSite` in an out-of-process COM surrogate. It reads small current-user registry values on the menu-construction path. It does not load the managed app, start Claude, scan files or contact the network while opening a menu.
- A signed sparse identity package registers folder and folder-background contexts. Desktop backgrounds use their actual Explorer folder view and are distinguished from ordinary folder backgrounds.
- Each Explorer context registers a single adaptive entry. Sixty-four stable binding slots support configuration changes without rebuilding or reinstalling the identity package. One shared background handler distinguishes the desktop from folder backgrounds, so preallocated or hidden verbs cannot force a flyout. Subcommands each contain an already-bound command and companion; task text stays out of registry command strings.
- Invocation resolves the selected folder or current view, validates the binding again and opens the managed task-input window. A stale open menu cannot invoke a newly substituted companion.
- Native DLL filenames include their content hash. Installation retains identical loaded files and places changed versions beside them; it does not overwrite an in-use DLL.
- The app manages only its own registration. Uninstalling an older copy does not remove a newer copy's registered menu.

## Build

The base WinForms app still builds with .NET Framework. Native menus additionally require an x64 LLVM-MinGW compiler, Windows SDK MakeAppx/SignTool, Python with Pillow, and a code-signing certificate available to the current user's signing store.

```powershell
$env:AGENT_PAL_SHELL_COMPILER = 'C:\tools\llvm-mingw\bin\x86_64-w64-mingw32-clang++.exe'
$env:AGENT_PAL_MAKEAPPX = 'C:\tools\windows-sdk\x64\makeappx.exe'
$env:AGENT_PAL_SHELL_SIGNER = '<your-code-signing-certificate-thumbprint>'
.\build.ps1
```

`tools/BuildShellExtension.ps1 -Unsigned` is for compile/manifest checks only. Unsigned executable identity packages cannot be enabled. Keep signing keys out of source archives and releases. The output includes runtime license notices, not the compiler or SDK tools.

## Validation

`tests/native-shell-test.ps1` verifies the actual x64 DLL, COM interfaces, titles/icons, enabled/hidden states, Unicode paths, invocation, and changed-binding protection. With the compiler provided, it also uses a test-only Explorer site to check both desktop/folder backgrounds and an invocation with no selected items. It launches a fixture program, never Claude, and restores the reserved test registry slot.

```powershell
.\tests\native-shell-test.ps1 -Dll '<compiled-shell-dll>' -Compiler $env:AGENT_PAL_SHELL_COMPILER
.\tests\quick-task-test.ps1
```

Native protocol tests and package signing are separate from visual acceptance in Explorer. A signed package must also register successfully on the target machine before the main-menu feature is considered fully accepted.

After registering the signed identity package, add `-Registered` to the native test command to exercise Windows' registered out-of-process COM activation, including invocation and background-site marshaling. Without this switch the test loads the DLL directly. Both modes use a reserved temporary binding and a fixture executable, not Claude.

## Offline deployment

The preview public certificate is `shell/agent-pal-preview.cer`. Its SHA-1 thumbprint is `28144B7235A5F7EEE8886478791135913C8FA0EC`, and it expires on February 18, 2027. This certificate signs the native menu DLL and identity package; it does not sign the main application or setup executable. Never distribute its private key.

On each target Windows 11 x64 machine, an administrator must first approve the public certificate in **Local Computer → Trusted People**. This can be done offline. Install the preview, then enable the main menu in command settings. Organizational application-control policies can impose additional requirements. Verify the package and menu on the actual target machine.

For long-term internal deployment, use an organization-approved code-signing identity and distribute its trust through the organization's normal process. A production certificate can be used during testing and release alike; self-signing is not a requirement of testing. The current preview has no trusted timestamp: certificate expiry affects new installation or reinstallation of its identity package. See [Microsoft's timestamping guidance](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview#timestamping).

References: [Microsoft: Explorer commands and package registration](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer), [identity packages with external location](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [IExplorerCommand](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iexplorercommand).
