#!/usr/bin/env bash
set -euo pipefail
case "$(uname -s)" in
    Linux) ;;
    MINGW*|MSYS*|CYGWIN*) exec powershell.exe -NoProfile -File "$(cygpath -w "$(dirname "${BASH_SOURCE[0]}")/build-outside.ps1")" ;;
    *) printf 'Run build-outside.ps1 in PowerShell on Windows; this shell script requires Linux.\n' >&2; exit 1 ;;
esac
command -v dotnet >/dev/null || { printf 'dotnet is required\n' >&2; exit 1; }
command -v macrodeck-plugin >/dev/null || { printf 'macrodeck-plugin is required\n' >&2; exit 1; }
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
build_root="$(cd -- "$repo/.." && pwd)/MacroDeck-builds/slideshow"
stage="$build_root/stage"
project=ImageSlideshow
mkdir -p -- "$build_root"
rm -rf -- "$stage"
mkdir -p -- "$stage/src/$project"
cp -- "$repo/Directory.Build.props" "$repo/Directory.Packages.props" "$repo/NuGet.config" "$stage/"
cp -R -- "$repo/src/$project/." "$stage/src/$project/"
rm -rf -- "$stage/src/$project/bin" "$stage/src/$project/obj" "$stage/src/$project/.macrodeck-dev-state"
macrodeck-plugin build --source "$stage/src/$project" --output "$build_root/artifacts" --force
