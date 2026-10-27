# Moorkyz local control GUI

This is an opt-in Windows front end for the repository-management CLI, not a
portal page, a Visory redesign, or a separately published tool.

From the intended project root:

```powershell
node .\moorkyz gui
# Uses the other launcher's configuration/state instead:
node .\src\.moorkyz\scripts\tools.moorkyz.com.cjs gui
```

Requires the existing Node runtime, Windows PowerShell 5.1 and Windows' built-in
WPF assemblies. No NuGet package, service, browser bridge, profile edit or
execution-policy change is added. A policy or Application Control error remains
an error; the launcher never bypasses it.

## What works

- Select a configured project, then explicitly run Start, Status, Restart, Stop,
  Logs, Doctor, Build, Test, Lint or Typecheck through the existing CLI.
- Commands run outside the UI thread, retain their real exit code, and show
  bounded stdout/stderr. Recent stderr is repeated on failure so a large stdout
  stream cannot conceal the error. Only one command runs at a time.
- Nothing executes on opening the window. Closing while a command is active is
  blocked with a visible explanation. Closing an idle GUI does not stop existing
  managed services; use Stop deliberately. No install, archive, scheduled task,
  arbitrary shell command or automatic prerequisite operation is exposed.
- Drag quick-action cards to reorder them for the current window. Alt + arrow
  keys gives the same reorder operation without dragging. Reordering does not
  execute a command or persist preferences.
- The Interaction lab offers three presets in each family: hover, press, drag
  and move. Badges and icon buttons use the same feedback system as other
  buttons. Material samples use real WPF 3D rotations, a fine tiled texture,
  an embossed imprint and pointer-positioned warm sunlight.
- The cursor-following drag preview uses a damped spring with velocity, so it
  trails acceleration, brakes on direction changes, and settles rather than
  teleporting. Its rendering subscription exists only during a drag and is
  removed on completion/cancellation.
- Windows' animation preference is respected. The Interface motion switch
  immediately clears active effects without removing focus/press/hover feedback
  or either reordering method.

## Shared implementation

- [Theme.xaml](Theme.xaml): central palette, spacing, radii, focus treatment and
  reusable button/card/badge/icon/select/input/scrollbar styles.
- [MainWindow.xaml](MainWindow.xaml): native window layout, workspace and lab.
- [MoorkyzGui.cs](MoorkyzGui.cs): shared control factories, interaction presets,
  project/action binding, asynchronous CLI output and cleanup.
- [Motion.cs](Motion.cs): inertial drag preview and reusable 3D material cards.
- [start.ps1](start.ps1): the STA native bootstrap. The CLI supplies validated
  projects and the selected entrypoint, preserving each launcher's namespace.

The GUI loads the existing [public icon set](../public). When copying optional
GUI support with the CLI, include both this directory and that public directory.
The portable CLI itself continues to work without GUI support.

## Verification

```powershell
node --test tests\tooling\moorkyz-gui.test.cjs
```

The Windows-only native fixture compiles the actual source and XAML, checks all
twelve preset choices and visible hover/press states, 3D and reduced-motion
cleanup, native button automation, reordering, spring lag/braking/settling,
bounded errors and real CLI GUI launch/close. Commands use synthetic temporary
files; tests never start a real project service or touch account/media data.

This is a first working UI pass. It does not claim independent accessibility
certification, real-account validation, a published desktop release, or a final
Moorkyz design. Text scaling/assistive-technology and real pointer-drag feel still
benefit from owner review.
