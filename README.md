# CandiHub

A Unity 6 (`6000.5.6f1`) URP 2D project.

## Requirements

- **Unity `6000.5.6f1`** — install this exact version through Unity Hub. The
  version is pinned in `ProjectSettings/ProjectVersion.txt`; opening the project
  with a different version will silently upgrade and reserialize every asset,
  which produces enormous diffs for everyone else.

## First-time setup (do this on every machine, macOS and Windows)

### 1. Clone

```bash
git clone https://github.com/Yana205/CandiHub.git
cd CandiHub
```

Only `Assets/`, `Packages/` and `ProjectSettings/` are tracked. Unity rebuilds
`Library/` (~2 GB), `Temp/`, `Logs/` and `UserSettings/` on first open, so the
first launch takes a few minutes. That is expected.

### 2. Install Git LFS — do this BEFORE cloning

This project stores binary assets (images, audio, models, fonts, native
plugins) in [Git LFS](https://git-lfs.com). Without it installed, those files
arrive as ~130-byte text placeholders instead of real content, and Unity
imports them as corrupt assets. Git reports no error, which makes this
confusing to diagnose.

**macOS**

```bash
brew install git-lfs
git lfs install
```

**Windows** — Git for Windows usually bundles LFS already, so check first:

```powershell
git lfs version
```

If that fails, install it, then run `git lfs install`:

```powershell
winget install --id GitHub.GitLFS -e     # or: choco install git-lfs
git lfs install
```

`git lfs install` is the step people skip. Installing the binary only puts it
on your PATH; this command writes the `filter.lfs.*` entries into your global
Git config, and those filters are what swap pointers for real content on
checkout. Run it once per machine.

**If you already cloned without LFS**, fix it with:

```bash
git lfs install
git lfs pull
```

### 3. Enable Unity's scene merge tool

Scenes (`.unity`) and prefabs (`.prefab`) are YAML. Git's line-based merge
corrupts them. Unity ships `UnityYAMLMerge` to merge them structurally instead.
`.gitattributes` already routes these files to a driver named `unityyamlmerge` —
this step tells Git where that program lives on your machine. Run it once:

Note the macOS path is `Contents/Helpers/`, not the `Contents/Tools/` that most
older guides list — Unity moved it. The Windows path below is the documented
location; confirm the file exists before running the command.

**macOS** (verified on this machine)

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/Helpers/UnityYAMLMerge merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config merge.unityyamlmerge.recursive binary
```

**Windows** (Git Bash or PowerShell)

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config merge.unityyamlmerge.recursive binary
```

These are `--local` settings, stored in `.git/config` — they are not committed,
because the path to Unity differs per machine and per OS.

### 4. Leave line endings to `.gitattributes`

Do **not** set `core.autocrlf`. `.gitattributes` already normalizes endings for
every file type in this project, and a global `autocrlf` setting can fight it.
If you previously set one, clear it:

```bash
git config --global --unset core.autocrlf
```

## Working agreements

**Never commit `Library/`.** It is a machine-local import cache, roughly 2 GB,
and it is already ignored. If you ever see it in `git status`, something is
wrong with `.gitignore` — stop and fix that first.

**Always commit `.meta` files alongside their assets.** Unity stores each
asset's GUID in its `.meta` sidecar, and every scene and prefab references
assets by GUID. Committing `Player.png` without `Player.png.meta` gives the
next person a broken reference, and Unity generates a *new* GUID on their
machine, which then conflicts with yours.

**Coordinate on scenes.** Even with `UnityYAMLMerge`, two people editing the
same scene at the same time is the main source of pain in Unity teams. Prefer
splitting work into separate prefabs, or agree who owns a scene before editing.

**Binary assets go through LFS automatically.** The patterns are already in
`.gitattributes`, so `git add` on a `.png`, `.wav`, `.fbx` or `.psd` routes it
to LFS with no extra command. Check what LFS is holding with `git lfs ls-files`.

**If you add a binary format not already listed**, add it to `.gitattributes`
*before* committing the first such file:

```bash
git lfs track "*.aseprite"
git add .gitattributes
```

Adding the rule afterwards does not move the already-committed file into LFS —
that needs a history rewrite (`git lfs migrate import`), which forces everyone
to re-clone. Rules first, files second.

**Watch the LFS quota.** GitHub's free tier includes 1 GB of LFS storage and
1 GB of bandwidth per month, and *every version* of every binary counts toward
storage. Large textures re-exported repeatedly will burn through it. Check
usage under Settings → Billing.

**File name casing matters.** macOS and Windows treat `Player.cs` and
`player.cs` as the same file; Linux and Git do not. Never rename a file by case
alone — rename it to something else first, commit, then rename to the final
name.

## Project settings that must not change

These are committed in `ProjectSettings/EditorSettings.asset` and keep the repo
diffable across platforms:

| Setting | Value | Why |
| --- | --- | --- |
| Asset Serialization Mode | `Force Text` | Binary scenes cannot be diffed or merged at all |
| Line Endings For New Scripts | `Unix` | New `.cs` files get LF regardless of who creates them |
