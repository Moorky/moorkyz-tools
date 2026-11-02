# Windows EXE launcher

Build an unsigned, icon-bearing **windowed** launcher for the same
[GUI-first startup](../scripts/README.md#shared-startup) used by
`RUN.cmd`, bare `node moorkyz` and `moorkyz start`. The
[Moorkyz control GUI](../gui/README.md) opens before the checklist:

```powershell
powershell.exe -NoProfile -File .\src\scripts\deployment\build-moorkyz-exe.ps1
& .\src\.moorkyz\app\dist\Moorkyz.exe
```

The local builder uses Windows' installed .NET Framework C# compiler and the
existing [project icon](../public/favicon.ico). It adds no dependencies. The
default output is ignored by Git. It refuses to overwrite an existing output;
choose a new path or explicitly remove your previous build.

```powershell
powershell.exe -NoProfile -File .\src\scripts\deployment\build-moorkyz-exe.ps1 `
  -OutputPath 'C:\my-builds\Moorkyz.exe' -IconPath 'C:\my-icons\app.ico'
& 'C:\my-builds\Moorkyz.exe' --root 'C:\my-checkout\moorkyz-tools'
# Run checks without installing workspace packages or starting a service:
& 'C:\my-builds\Moorkyz.exe' --root 'C:\my-checkout\moorkyz-tools' --check
```

Paths passed to the builder resolve against the caller's working directory.
The EXE discovers its checkout from its own directory and ancestors, not the
working directory. Use `--root` when keeping the EXE outside the checkout.
It requires Node.js 22.18.0+ on PATH and the checkout. The shared checklist checks
the declared pnpm/Next.js/React/TypeScript stack and workspace dependencies.
Windows PowerShell/WPF is required for GUI startup. Node is resolved only
from absolute PATH entries, not implicitly from the current directory.

This is a convenience launcher, **not** a bundled portal, a standalone desktop
release or an installer. Opening it runs `node moorkyz launch` through the shared
CLI without a persistent terminal window. Missing
workspace dependencies can be installed from the frozen lockfile, with at most
two attempts. Errors are task histories with fix suggestions. `--check` uses the
same checks without dependency installation or GUI; failed checks still write
error tasks. It exposes captured output to a calling automation process and
preserves the shared command's exit code. Normal bootstrap failures use a
visible native error dialog rather than disappearing into a hidden console.
RUN prefers this built EXE; without a build, its hidden PowerShell
[bootstrap](launch.ps1) opens the same GUI and displays bootstrap errors.
No execution-policy bypass is used; organizational restrictions still apply.

The builder and launcher do not install machine-wide prerequisites, change execution policy,
create shortcuts, modify profiles, register scheduled tasks, request elevation,
sign, upload, publish or deploy to a remote host. The unsigned EXE may trigger
Windows SmartScreen or organizational application-control policy; these are not
bypassed. Rebuild after changing the launcher source or icon. Keep the checkout
and CLI/GUI sources with it; do not distribute only the EXE as a complete app.

## Verification

From the repository root:

```powershell
node --test --test-concurrency=1 src\tests\tooling\moorkyz-startup.test.cjs src\tests\tooling\moorkyz-exe.test.cjs src\tests\tooling\moorkyz-gui.test.cjs
```

The Windows-only EXE test compiles a real launcher in an isolated temporary
directory, checks its windowed subsystem and embedded icon bytes, verifies overwrite
and invalid-icon failures, and exercises root discovery, runtime checks and startup
argument forwarding with a synthetic runtime. Shared startup tests verify bounded
repair/retry behavior and task histories. The adjacent GUI tests compile and
exercise the actual WPF implementation. No real project service is started.
