#!/usr/bin/env bash
set -eu

# Minimal subset of the helper interface used by the known-working Makefile.
# CosmoNote only needs the Windows profile-path commands.

windows_home() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "${HOME:?HOME is not set}"
        return
    fi

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
