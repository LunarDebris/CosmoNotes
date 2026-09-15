# MoonVault local build settings.
#
#   cp setup.mk.example setup.mk
#
# setup.mk is git-ignored and optional - the Makefile includes it with `-include`, so nothing
# breaks if it's absent. Use it for machine-specific paths instead of editing the Makefile or
# exporting variables into your shell (which, under MSYS/Git Bash, often don't survive the
# trip to the native dotnet.exe anyway).
#
# Precedence:  command line  >  setup.mk  >  environment  >  auto-detection.

# --- Android / Java toolchain ---------------------------------------------------------------
#
# Set these if auto-detection picks the wrong one, or finds nothing. Use WINDOWS-style paths
# (D:\...) when building on Windows. Quotes aren't needed, and a trailing slash is stripped
# for you.
#
# A JDK must contain bin/jar - a JRE will be rejected by .NET for Android with error XA5300.
# .NET 10's Android SDK expects JDK 21.

JAVA_HOME   = D:\Android\SDK\jdk\jdk-21
ANDROID_HOME = D:\Android\SDK

# These take priority over JAVA_HOME / ANDROID_HOME if you'd rather be explicit:
# JAVA_SDK    = D:\Android\SDK\jdk\jdk-21
# ANDROID_SDK = D:\Android\SDK

# --- Windows build ---------------------------------------------------------------------------

# Runtime identifier for `make build-windows`.
# WIN_RID = win-x64

# Bundle the .NET runtime into the published output (bigger, but runs anywhere).
# SELF_CONTAINED = true

# --- Paths ------------------------------------------------------------------------------------

# Where published builds are written.
# ARTIFACTS = artifacts

# Project locations, if you ever move them.
# SOLUTION = MoonVault.sln
# DESKTOP  = Desktop/MoonVault.Desktop.csproj
# ANDROID  = Android/MoonVault.Android.csproj
# IOS      = iOS/MoonVault.iOS.csproj

# --- Anything else ----------------------------------------------------------------------------
#
# This is a normal makefile fragment, so any variable the Makefile uses can be set here.

KEYSTORE      := D:/Development/certs/mooncrash.keystore
KEY_ALIAS     := mooncrash
KEYSTORE_PASS := MOONCRASH$2026
STORE_PASS := MOONCRASH$2026
KEY_PASS      := MOONCRASH$2026
NAME = MoonVault