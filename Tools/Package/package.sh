#!/bin/sh
# Builds Game Modes and zips the installed mod folder to ~/Downloads, named with the time it was built
# to the minute: GameModes-2026-10-23-11h02.zip. Pass another folder to zip into it.
# The older GameModes zips there are deleted, so only the newest is left.
set -e
repo="$(cd "$(dirname "$0")/../.." && pwd)"
mods="$HOME/Documents/Timberborn/Mods"
# Absolute, since the zip is written from inside the Mods folder
out="$(cd "${1:-$HOME/Downloads}" && pwd)"
dotnet build "$repo/GameModes/GameModes.csproj" -c Release -nologo -v q
zip="$out/GameModes-$(date +%Y-%m-%d-%Hh%M).zip"
rm -f "$zip"
(cd "$mods" && zip -qr "$zip" GameModes -x "*.DS_Store")
for old in "$out"/GameModes-[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]-[0-9][0-9]h[0-9][0-9].zip; do
    if [ -e "$old" ] && [ "$old" != "$zip" ]; then rm -f "$old"; fi
done
echo "$zip"
