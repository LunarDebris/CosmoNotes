APP      := CosmoNotes
PROJECT  := CosmoBoard.csproj
CONFIG   ?= Release

SHELL := /bin/bash

UNAME_S := $(shell uname -s 2>/dev/null)
IS_MSYS := $(filter MINGW% MSYS% CYGWIN%,$(UNAME_S))

DETECT_ENV := bash scripts/detect-toolchain.sh

ifneq (,$(IS_MSYS))
  override USERPROFILE  := $(shell $(DETECT_ENV) userprofile)
  override APPDATA      := $(shell $(DETECT_ENV) appdata)
  override LOCALAPPDATA := $(shell $(DETECT_ENV) localappdata)
  export USERPROFILE
  export APPDATA
  export LOCALAPPDATA
  export ProgramFiles
  export ProgramData

  override PATHEXT := $(if $(strip $(PATHEXT)),$(PATHEXT),.COM;.EXE;.BAT;.CMD;.VBS;.JS;.WS;.WSF;.MSC)
  export PATHEXT

  export MSYS2_ARG_CONV_EXCL := *
  export MSYS_NO_PATHCONV := 1

  ifneq ($(strip $(LOCALAPPDATA)),)
    export TEMP := $(LOCALAPPDATA)\Temp
    export TMP  := $(LOCALAPPDATA)\Temp
  endif
endif

.PHONY: restore build run clean publish-win publish-linux publish-all doctor increment-build publish-win-no-increment publish-linux-no-increment

restore:
	dotnet restore $(PROJECT)

build: restore
	dotnet build $(PROJECT) -c $(CONFIG) --no-restore

run: build
	dotnet ./bin/$(CONFIG)/net8.0/$(APP).dll

increment-build:
	bash scripts/increment-build.sh

publish-win: increment-build publish-win-no-increment

publish-linux: increment-build publish-linux-no-increment

publish-win-no-increment:
	dotnet publish $(PROJECT) -c $(CONFIG) -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin/publish/win-x64

publish-linux-no-increment:
	dotnet publish $(PROJECT) -c $(CONFIG) -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin/publish/linux-x64

# One release number is shared by both platform packages.
publish-all: increment-build publish-win-no-increment publish-linux-no-increment

clean:
	rm -rf bin obj .nuget NuGet

doctor:
	@echo "uname:        $(UNAME_S)"
	@echo "MSYS-style:   $(if $(IS_MSYS),yes,no)"
	@echo "USERPROFILE:  $(if $(USERPROFILE),$(USERPROFILE),<empty>)"
	@echo "APPDATA:      $(if $(APPDATA),$(APPDATA),<empty>)"
	@echo "LOCALAPPDATA: $(if $(LOCALAPPDATA),$(LOCALAPPDATA),<empty>)"
	@echo "ProgramFiles: $(if $(ProgramFiles),$(ProgramFiles),<empty>)"
	@echo "ProgramData:  $(if $(ProgramData),$(ProgramData),<empty>)"
	@echo "PATHEXT:      $(if $(PATHEXT),$(PATHEXT),<empty>)"
	@echo "TEMP:         $(if $(TEMP),$(TEMP),<empty>)"
	@echo "TMP:          $(if $(TMP),$(TMP),<empty>)"
