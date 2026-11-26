# Safe LogistiCore updater (Windows PowerShell)

The repository-root `update-logisticore.ps1` is an **opt-in local** Git helper. GitHub cannot remotely pull files into a user's Windows Unity folder; the user executes this script on their own computer.

## Installation / first-time use
1. Download `update-logisticore.ps1` from the GitHub PR's **Files changed** view to a folder **outside** the Unity repository (e.g. Downloads). Keeping it outside the worktree avoids a self-inflicted untracked-file safety stop.
2. Save and close Unity. Avoid running multiple Unity processes during Git operations.
3. Read the script before executing. In PowerShell:
   ```powershell
   & "$env:USERPROFILE\Downloads\update-logisticore.ps1" -CheckOnly
   & "$env:USERPROFILE\Downloads\update-logisticore.ps1"
   ```
   If PowerShell's execution policy blocks it, use a per-process command after reviewing the file, not a system-wide policy change:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\update-logisticore.ps1"
   ```
4. Target a specific newer branch only when instructed:
   ```powershell
   & "$env:USERPROFILE\Downloads\update-logisticore.ps1" -TargetBranch "feature/depot-visual-06-yard-grounds-and-inspection"
   ```
   The default target is the current approved depot-development branch, not `main`.

## Exact safety behavior
- Checks the chosen repository directory, exact `hansaglam/unitylogisticore` GitHub origin, target branch and Git executable.
- **Stops before fetching or switching** when *any* tracked or untracked file is present. Unity-generated `DepotPrototype.unity`, sprite assets, `.meta` files and `ProjectSettings` are NOT discarded.
- Warns and stops if Unity Editor is running.
- After a clean-worktree check, fetches origin, verifies the remote target branch, switches to it if needed and runs `git pull --ff-only origin <branch>`. No force checkout, reset, clean, automatic stash, push, merge commit or auto-PR.
- `-CheckOnly` inspects the local repository without changing anything.

## User's existing dirty Unity working tree
When the utility reports local changes, do NOT force it to continue. The safe options are:
- **Preserve as a real commit** in an appropriate development branch after checking generated files (especially scene and `.meta` files); or
- Make an explicit external backup and manually run:
  ```powershell
  git -C "D:\Projects\unitylogisticore" stash push -u -m "Before LogistiCore development update"
  ```
  Then run the updater and separately inspect/reapply the stash only after verifying branch compatibility. This helper deliberately refuses to make that choice on the user's behalf.

## Limitations
- It updates the **local** repository; it does NOT push local changes to GitHub or merge pull requests automatically.
- No Windows/Unity end-to-end test was run by the GitHub authoring tool environment. Check the actual terminal output before relying on it broadly.
- If the latest feature is only on a separate GitHub draft PR, pass that PR's **head branch**, not `main`, until it is reviewed and merged.
