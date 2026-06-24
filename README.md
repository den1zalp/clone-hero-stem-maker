# Stem Maker WinForms

Windows GUI for generating Clone Hero stems from single-audio song folders.

## What The App Does

- Checks/installs Python 3.12 through `winget`.
- Checks/installs FFmpeg through `winget`.
- Creates a local `.venv`.
- Installs CUDA PyTorch, Demucs, and SoundFile into `.venv`.
- Scans a Clone Hero `Songs` folder with `ch_stem_batcher.py --dry-run`.
- Shows processable songs in a checkbox list.
- Processes only the selected song folders.
- Cleans `_stem_batcher_backup` folders when the user confirms.

## Build

Install the .NET SDK, then double-click:

```text
Build Release.bat
```

The published GUI will be here:

```text
bin\Release\net8.0-windows\win-x64\publish\Stem Maker.exe
```

Keep `ch_stem_batcher.py` next to the exe. The project copies it automatically on build.
