{
  description = "Flake for developing Rimworld Skunks";

  inputs = {
    # Pinned to the cached unstable channel rather than nixpkgs master.
    nixpkgs.url = "github:nixos/nixpkgs/nixpkgs-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils, ... }@inputs:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = inputs.nixpkgs.legacyPackages.${system};

        # Where Steam puts RimWorld, and where the managed DLLs live inside it.
        # Both can be overridden by exporting RIMWORLD_DIR before entering the shell.
        steamApps =
          if pkgs.stdenv.hostPlatform.isDarwin
          then "$HOME/Library/Application Support/Steam/steamapps"
          else "$HOME/.steam/steam/steamapps";
        # From 1.7 the game runs on .NET 10 instead of Unity/Mono. On macOS the install folder
        # holds Data/, Mods/ and Version.txt, and the game's assemblies sit in RimWorld.app.
        # (The Linux layout for 1.7 is not known yet; this is the 1.6 one.)
        rimworldDir = "${steamApps}/common/RimWorld";
        managedSubdir =
          if pkgs.stdenv.hostPlatform.isDarwin
          then "RimWorld.app/Contents/MacOS"
          else "RimWorldLinux_Data/Managed";

        # Decompile the game's Assembly-CSharp.dll into a browsable C# project.
        # Output is keyed on the game version so 1.6/1.7 trees can sit side by side.
        rw-decompile = pkgs.writeShellScriptBin "rw-decompile" ''
          set -euo pipefail
          version="$(tr -d '\r\n' < "$RIMWORLD_DIR/Version.txt" | cut -d' ' -f1)"
          out="''${1:-''${XDG_CACHE_HOME:-$HOME/.cache}/rimworld-decompiled/$version}"
          mkdir -p "$out"
          echo "Decompiling RimWorld $version -> $out" >&2
          ${pkgs.ilspycmd}/bin/ilspycmd --project --outputdir "$out" \
            --referencepath "$RIMWORLD_MANAGED" \
            "$RIMWORLD_MANAGED/Assembly-CSharp.dll"
          echo "$out"
        '';

        # Symlink this checkout into the game's Mods folder so it shows up in the
        # mod list (and so the csproj's ../../Contents HintPaths resolve from there).
        rw-link = pkgs.writeShellScriptBin "rw-link" ''
          set -euo pipefail
          src="$(pwd -P)"
          dest="$RIMWORLD_DIR/Mods/$(basename "$src")"
          if [ ! -f "$src/About/About.xml" ]; then
            echo "rw-link: run from the mod root (no About/About.xml here)" >&2
            exit 1
          fi
          ln -sfn "$src" "$dest"
          echo "$dest -> $src"
        '';
      in
      {
        devShells.default = pkgs.mkShell {
          packages = with pkgs; [
            # RimWorld 1.6 runs Unity 2022.3 / Mono; mods are net472/net48 class
            # libraries. The SDK builds those via the NETFramework reference
            # assemblies it restores from NuGet, so no mono/msbuild is needed.
            dotnetCorePackages.sdk_10_0
            ilspycmd
            rw-decompile
            rw-link
          ];

          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";

          shellHook = ''
            export RIMWORLD_DIR="''${RIMWORLD_DIR:-${rimworldDir}}"
            export RIMWORLD_MANAGED="$RIMWORLD_DIR/${managedSubdir}"
            export RIMWORLD_WORKSHOP="''${RIMWORLD_WORKSHOP:-${steamApps}/workshop/content/294100}"
            export RIMWORLD_HARMONY="$RIMWORLD_WORKSHOP/2009463077/1.7/Assemblies"
          '';
        };
      }
    );
}
