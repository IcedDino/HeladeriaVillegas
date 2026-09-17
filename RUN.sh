#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script requires macOS. On Windows, use RUN.ps1."
    exit 1
fi

if [[ -z "${DEVELOPER_DIR:-}" ]]; then
    selected_developer_dir="$(xcode-select -p)"
    if [[ "$selected_developer_dir" == *.app/Contents/Developer ]]; then
        export DEVELOPER_DIR="$selected_developer_dir"
    elif [[ -d /Applications/Xcode.app ]]; then
        export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
    elif [[ -d /Applications/Xcode-beta.app ]]; then
        export DEVELOPER_DIR=/Applications/Xcode-beta.app/Contents/Developer
    else
        echo "Install Xcode before running this app."
        exit 1
    fi
fi

case "$(uname -m)" in
    arm64) runtime=maccatalyst-arm64 ;;
    x86_64) runtime=maccatalyst-x64 ;;
    *) echo "Unsupported Mac architecture."; exit 1 ;;
esac

dotnet build HeladeriaPOS.Maui.csproj -f net8.0-maccatalyst -c Debug -p:RuntimeIdentifier="$runtime"
open "bin/Debug/net8.0-maccatalyst/$runtime/HeladeriaPOS.Maui.app"
