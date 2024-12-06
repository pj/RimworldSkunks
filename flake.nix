{
  description = "Flake for testing rimworld dev";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils, ... }@inputs:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = inputs.nixpkgs.legacyPackages.${system};
      in
      {
        # packages = {

        # };
        devShells.default = pkgs.mkShell {
            # packages = with pkgs; [

            # avalonia-ilspy
            # ];
          buildInputs = with pkgs; [
            dotnetCorePackages.sdk_9_0
          ];
        };
      }
    );
}