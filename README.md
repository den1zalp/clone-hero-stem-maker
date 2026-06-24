# Clone Hero Stem Maker

Clone Hero Stem Maker is a Windows desktop tool that scans Clone Hero song folders and converts single-audio charts into multitrack stem folders.

It is an unofficial community tool and is not affiliated with Clone Hero.

## What It Does

Many Clone Hero charts include only one audio file, such as `song.ogg`, `song.opus`, or `guitar.ogg`.

This app can split that single audio file into separate stems:

- `guitar.ogg`
- `rhythm.ogg`
- `song.ogg`

The app uses Demucs in the background to separate audio, then prepares the files in a Clone Hero-friendly format.

## How To Use

1. Open `Stem Maker.exe`.

2. Click `Setup runtime`.

   This checks and prepares the required tools:

   - Python 3.12
   - FFmpeg
   - PyTorch
   - Demucs
   - required Python audio packages

   The runtime is installed under:

   ```text
   %LOCALAPPDATA%\Stem Maker
   ```

3. Choose processing mode.

   - `GPU / CUDA`: faster, requires a supported NVIDIA GPU and compatible drivers.
   - `CPU`: slower, but works on more computers.

4. Click `Browse`.

   Select your Clone Hero songs/charts folder.

   Example:

   ```text
   D:\CloneHero-win-x64\Windows - Standalone\PlayerData\Songs
   ```

5. Click `Scan folder`.

   The app scans the selected folder and lists songs that appear to use a single audio file.

   Songs that already look like multitrack charts are skipped.

6. Select the songs you want to process.

   You can use:

   - `Select all`
   - `Select none`
   - individual checkboxes in the song list

7. Click `Make stems`.

   The selected songs will be processed one by one.

8. Optional: click `Clean backups`.

   During processing, the app may keep backup folders so the original audio can be restored if needed.

   `Clean backups` removes those backup folders after you confirm everything works.

## Building From Source

Requirements:

- Windows
- Visual Studio 2022 or newer
- .NET 8 Desktop Development workload

Steps:

1. Open the project in Visual Studio.

2. Open:

   ```text
   StemMaker.WinForms.csproj
   ```

3. Select `Release`.

4. Publish using the included publish profile:

   ```text
   SingleFile-win-x64
   ```

5. The output will be a single executable:

   ```text
   Stem Maker.exe
   ```

## Notes

- The app itself is a single-file Windows executable.
- Python, Demucs, and PyTorch are not bundled inside the exe because they are large.
- They are installed automatically by `Setup runtime`.
- GPU mode is recommended only for supported NVIDIA GPUs.
- If GPU mode fails, switch to CPU mode and run setup again.
