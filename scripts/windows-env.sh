#!/usr/bin/env bash
set -eu

# Print Windows-native profile paths for native tools launched from
# MSYS/MINGW/Cygwin. We intentionally derive these from HOME because the very
# problem this script fixes is that USERPROFILE/APPDATA/LOCALAPPDATA may be
# absent from the shell environment.

windows_home() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "${HOME:?HOME is not set}"
        return
    fi

    # Fallback for unusual shells where HOME is already a Windows path.
    case "${HOME:-}" in
        [A-Za-z]:\\*|[A-Za-z]:/*)
            printf '%s\n' "$HOME"
            ;;
        *)
            printf 'Unable to derive a Windows user profile from HOME=%s\n' "${HOME:-<empty>}" >&2
            exit 1
            ;;
    esac
}

profile="$(windows_home)"
# Normalize forward slashes if a fallback returned them.
profile="${profile//\//\\}"

case "${1:-}" in
    userprofile)
        printf '%s\n' "$profile"
        ;;
    appdata)
        printf '%s\\AppData\\Roaming\n' "$profile"
        ;;
    localappdata)
        printf '%s\\AppData\\Local\n' "$profile"
        ;;
    *)
        printf 'usage: %s {userprofile|appdata|localappdata}\n' "$0" >&2
        exit 2
        ;;
esac
