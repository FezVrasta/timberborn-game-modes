#!/bin/sh
# Builds Game Modes and zips the installed mod folder to ~/Downloads, named with the time it was built
# to the minute: GameModes-2026-10-23-11h02.zip. Pass another folder to zip into it.
set -e
repo="$(cd "$(dirname "$0")/../.." && pwd)"
mods="$HOME/Documents/Timberborn/Mods"
out="${1:-$HOME/Downloads}"
dotnet build "$repo/GameModes/GameModes.csproj" -c Release -nologo -v q
zip="$out/GameModes-$(date +%Y-%m-%d-%Hh%M).zip"
rm -f "$zip"
(cd "$mods" && zip -qr "$zip" GameModes -x "*.DS_Store")
echo "$zip"
