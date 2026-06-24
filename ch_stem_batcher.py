#!/usr/bin/env python3
"""
Batch-separate single-audio Clone Hero songs with Demucs.

Default output mapping:
  guitar.ogg = Demucs guitar stem
  rhythm.ogg = Demucs bass stem
  song.ogg   = Demucs vocals + drums + piano + other stems

This intentionally does not create drums.ogg or keys.ogg.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


AUDIO_EXTS = {".ogg", ".mp3", ".wav", ".flac", ".opus", ".m4a"}
IGNORED_AUDIO_STEMS = {"preview"}
KNOWN_CLONE_AUDIO_STEMS = {
    "song",
    "background",
    "guitar",
    "rhythm",
    "bass",
    "drums",
    "keys",
    "vocals",
}


def norm(text: str) -> str:
    return text if ARGS.case_sensitive else text.casefold()


def read_song_ini(song_dir: Path) -> dict[str, str]:
    ini = song_dir / "song.ini"
    data: dict[str, str] = {}
    if not ini.exists():
        return data

    try:
        for raw_line in ini.read_text(encoding="utf-8-sig", errors="replace").splitlines():
            line = raw_line.strip()
            if not line or line.startswith(("#", ";", "[")) or "=" not in line:
                continue
            key, value = line.split("=", 1)
            data[key.strip().casefold()] = value.strip()
    except OSError:
        return data

    return data


def is_song_folder(path: Path) -> bool:
    return (path / "song.ini").exists() and bool(primary_audio_files(path))


def is_audio_file(path: Path) -> bool:
    return path.is_file() and path.suffix.casefold() in AUDIO_EXTS


def primary_audio_files(song_dir: Path) -> list[Path]:
    files = []
    for path in song_dir.iterdir():
        if not is_audio_file(path):
            continue
        if path.stem.casefold() in IGNORED_AUDIO_STEMS:
            continue
        files.append(path)

    return sorted(files, key=lambda path: path.name.casefold())


def metadata_haystack(song_dir: Path, root: Path, ini: dict[str, str]) -> str:
    rel = song_dir.relative_to(root) if song_dir != root else song_dir.name
    bits = [
        str(rel),
        song_dir.name,
        ini.get("artist", ""),
        ini.get("name", ""),
        ini.get("title", ""),
        ini.get("album", ""),
    ]
    return "\n".join(bits)


def passes_filters(song_dir: Path, root: Path, ini: dict[str, str]) -> bool:
    if ARGS.match:
        haystack = norm(metadata_haystack(song_dir, root, ini))
        if not all(norm(item) in haystack for item in ARGS.match):
            return False

    if ARGS.artist:
        artist = norm(ini.get("artist", ""))
        fallback = norm(str(song_dir.relative_to(root)))
        if not all(norm(item) in artist or norm(item) in fallback for item in ARGS.artist):
            return False

    if ARGS.song:
        song_name = norm("\n".join([ini.get("name", ""), ini.get("title", ""), song_dir.name]))
        if not all(norm(item) in song_name for item in ARGS.song):
            return False

    return True


def classify_audio(song_dir: Path) -> tuple[str, Path | None, str]:
    audio_files = primary_audio_files(song_dir)

    if len(audio_files) == 1:
        return "process", audio_files[0], "single audio"

    if not audio_files:
        return "skip", None, "no supported audio file"

    known_stems = {path.stem.casefold() for path in audio_files} & KNOWN_CLONE_AUDIO_STEMS
    if len(audio_files) > 1 and known_stems:
        return "skip", None, "already looks multitrack"

    return "skip", None, f"multiple audio files ({len(audio_files)})"


def find_stem_dir(output_root: Path, source: Path) -> Path:
    expected = output_root / ARGS.model / source.stem
    if expected.exists():
        return expected

    matches = list(output_root.rglob(source.stem))
    if len(matches) == 1 and matches[0].is_dir():
        return matches[0]

    raise RuntimeError(f"Could not find Demucs output folder for {source.name}")


def run(cmd: list[object]) -> None:
    cmd = [str(part) for part in cmd]
    if ARGS.verbose:
        print("RUN", " ".join(cmd))
    subprocess.run(cmd, check=True)


def ffmpeg_convert(input_file: Path, output_file: Path) -> None:
    run(
        [
            ARGS.ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(input_file),
            "-map",
            "0:a:0",
            "-c:a",
            "libvorbis",
            "-q:a",
            str(ARGS.ogg_quality),
            str(output_file),
        ]
    )


def ffmpeg_mix(inputs: list[Path], output_file: Path) -> None:
    cmd = [ARGS.ffmpeg, "-hide_banner", "-loglevel", "error", "-y"]
    for input_file in inputs:
        cmd.extend(["-i", str(input_file)])

    filter_inputs = "".join(f"[{i}:a:0]" for i in range(len(inputs)))
    filter_graph = f"{filter_inputs}amix=inputs={len(inputs)}:duration=longest:normalize=0[a]"
    cmd.extend(
        [
            "-filter_complex",
            filter_graph,
            "-map",
            "[a]",
            "-c:a",
            "libvorbis",
            "-q:a",
            str(ARGS.ogg_quality),
            str(output_file),
        ]
    )
    run(cmd)


def backup_existing(song_dir: Path, files: list[Path]) -> None:
    unique_files = list(dict.fromkeys(files))
    existing = [path for path in unique_files if path.exists()]
    if not existing:
        return

    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_dir = song_dir / "_stem_batcher_backup" / stamp
    backup_dir.mkdir(parents=True, exist_ok=True)

    for path in existing:
        shutil.move(str(path), str(backup_dir / path.name))


def process_song(song_dir: Path, source: Path) -> None:
    print(f"PROCESS {song_dir}")

    with tempfile.TemporaryDirectory(prefix="ch-stem-batcher-") as temp_name:
        temp_dir = Path(temp_name)
        demucs_out = temp_dir / "demucs"
        final_out = temp_dir / "final"
        final_out.mkdir()

        run(
            [
                *ARGS.demucs,
                "-n",
                ARGS.model,
                "-d",
                ARGS.device,
                "-o",
                str(demucs_out),
                str(source),
            ]
        )

        stem_dir = find_stem_dir(demucs_out, source)
        guitar = stem_dir / "guitar.wav"
        bass = stem_dir / "bass.wav"

        backing_names = ["vocals.wav", "drums.wav", "piano.wav", "other.wav"]
        backing = [stem_dir / name for name in backing_names if (stem_dir / name).exists()]

        missing = [str(path.name) for path in [guitar, bass] if not path.exists()]
        if missing:
            raise RuntimeError(f"Missing required stem(s) in {stem_dir}: {', '.join(missing)}")
        if not backing:
            raise RuntimeError(f"No backing stems found in {stem_dir}")

        ffmpeg_convert(guitar, final_out / "guitar.ogg")
        ffmpeg_convert(bass, final_out / "rhythm.ogg")
        ffmpeg_mix(backing, final_out / "song.ogg")

        backup_existing(
            song_dir,
            primary_audio_files(song_dir)
            + [song_dir / "song.ogg", song_dir / "guitar.ogg", song_dir / "rhythm.ogg"],
        )

        for name in ["guitar.ogg", "rhythm.ogg", "song.ogg"]:
            shutil.move(str(final_out / name), str(song_dir / name))


def collect_songs(root: Path) -> list[tuple[Path, dict[str, str]]]:
    songs: list[tuple[Path, dict[str, str]]] = []
    for dirpath, dirnames, _ in os.walk(root):
        path = Path(dirpath)
        dirnames[:] = [name for name in dirnames if name not in {"_stem_batcher_backup"}]
        try:
            if is_song_folder(path):
                songs.append((path, read_song_ini(path)))
        except OSError:
            continue
    return songs


def collect_selected_songs(root: Path, selected_dirs: list[Path]) -> list[tuple[Path, dict[str, str]]]:
    songs: list[tuple[Path, dict[str, str]]] = []
    seen: set[Path] = set()
    for raw_dir in selected_dirs:
        song_dir = raw_dir.expanduser()
        if not song_dir.is_absolute():
            song_dir = root / song_dir
        try:
            song_dir = song_dir.resolve()
        except OSError:
            continue
        if song_dir in seen:
            continue
        seen.add(song_dir)
        if song_dir.exists() and is_song_folder(song_dir):
            songs.append((song_dir, read_song_ini(song_dir)))
        else:
            print(f"SKIP    {song_dir} (not a valid Clone Hero song folder)")
    return songs


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Batch Demucs separation for single-audio Clone Hero songs."
    )
    parser.add_argument("--songs", required=True, type=Path, help="Clone Hero Songs root folder.")
    parser.add_argument("--match", action="append", help="Match folder path or song.ini metadata.")
    parser.add_argument("--artist", action="append", help="Match song.ini artist, with folder fallback.")
    parser.add_argument("--song", action="append", help="Match song.ini name/title or folder name.")
    parser.add_argument("--song-dir", action="append", type=Path, help="Process this exact song folder. Can be repeated.")
    parser.add_argument("--song-dir-file", type=Path, help="Text file with one exact song folder per line.")
    parser.add_argument("--all", action="store_true", help="Allow processing every matching single-audio folder.")
    parser.add_argument("--dry-run", action="store_true", help="Only list what would happen.")
    parser.add_argument("--apply", action="store_true", help="Actually write files.")
    parser.add_argument("--plan-json", type=Path, help="Write dry-run/scan results to this JSON file.")
    parser.add_argument("--limit", type=int, help="Process at most this many matching songs.")
    parser.add_argument("--model", default="htdemucs_6s", help="Demucs model name.")
    parser.add_argument("--device", default="cuda", help="Demucs device: cuda, cpu, or mps.")
    parser.add_argument(
        "--demucs",
        nargs="+",
        default=[sys.executable, "-m", "demucs"],
        help="Demucs command. Examples: --demucs demucs OR --demucs py -m demucs",
    )
    parser.add_argument("--ffmpeg", default="ffmpeg", help="FFmpeg executable.")
    parser.add_argument("--ogg-quality", default=6, type=int, help="Vorbis quality, usually 4-8.")
    parser.add_argument("--case-sensitive", action="store_true", help="Make filters case-sensitive.")
    parser.add_argument("--verbose", action="store_true", help="Print external commands.")
    return parser.parse_args()


def main() -> int:
    root = ARGS.songs.expanduser().resolve()
    if not root.exists():
        print(f"Songs folder does not exist: {root}", file=sys.stderr)
        return 2

    if ARGS.dry_run and ARGS.apply:
        print("Use either --dry-run or --apply, not both.", file=sys.stderr)
        return 2

    if not ARGS.dry_run and not ARGS.apply:
        print("Refusing to write without --apply. Use --dry-run first.", file=sys.stderr)
        return 2

    selected_dirs = list(ARGS.song_dir or [])
    if ARGS.song_dir_file:
        try:
            selected_dirs.extend(
                Path(line.strip())
                for line in ARGS.song_dir_file.read_text(encoding="utf-8-sig").splitlines()
                if line.strip()
            )
        except OSError as exc:
            print(f"Could not read --song-dir-file: {exc}", file=sys.stderr)
            return 2

    if ARGS.apply and not (ARGS.match or ARGS.artist or ARGS.song or selected_dirs or ARGS.all):
        print("Refusing to process everything without a filter. Add --match/--artist/--song/--song-dir/--song-dir-file or --all.", file=sys.stderr)
        return 2

    songs = collect_selected_songs(root, selected_dirs) if selected_dirs else collect_songs(root)
    matched = 0
    planned: list[tuple[Path, Path]] = []
    plan_rows: list[dict[str, str]] = []

    for song_dir, ini in songs:
        if not selected_dirs and not passes_filters(song_dir, root, ini):
            continue

        status, source, reason = classify_audio(song_dir)
        label = f"{ini.get('artist', '').strip()} - {ini.get('name', '').strip()}".strip(" -")
        label = label or str(song_dir.relative_to(root))

        plan_rows.append(
            {
                "label": label,
                "path": str(song_dir),
                "source": source.name if source else "",
                "status": status,
                "reason": reason,
            }
        )

        if status == "process" and source:
            print(f"MATCH   {label}")
            print(f"        source: {source.name}")
            planned.append((song_dir, source))
            matched += 1
            if ARGS.limit and matched >= ARGS.limit:
                break
        else:
            print(f"SKIP    {label} ({reason})")

    if ARGS.plan_json:
        ARGS.plan_json.parent.mkdir(parents=True, exist_ok=True)
        ARGS.plan_json.write_text(
            json.dumps(
                {
                    "songs_root": str(root),
                    "planned_count": len(planned),
                    "rows": plan_rows,
                },
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )

    if ARGS.dry_run:
        print(f"\nDry run complete. {len(planned)} song(s) would be processed.")
        return 0

    if not planned:
        print("No songs to process.")
        return 0

    for song_dir, source in planned:
        process_song(song_dir, source)

    print(f"\nDone. Processed {len(planned)} song(s).")
    return 0


ARGS = parse_args()


if __name__ == "__main__":
    raise SystemExit(main())
