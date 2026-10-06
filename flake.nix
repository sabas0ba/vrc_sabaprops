{
  description = "SabaProps offline Llama verification; nixpkgs pin follows dotfiles 9b9619ba";
  inputs.nixpkgs.url = "github:NixOS/nixpkgs/597283ad8aa0b331c788e97c4c262d58877074ef";
  outputs = { nixpkgs, ... }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
    in {
      devShells = nixpkgs.lib.genAttrs systems (system:
        let pkgs = import nixpkgs { inherit system; };
        in {
          default = pkgs.mkShell {
            packages = with pkgs; [ bashInteractive coreutils findutils gnused gawk git dotnet-sdk_8 glslang ];
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            shellHook = ''
              export DOTNET_CLI_HOME="$PWD/.verify/dotnet-home"
              export TMPDIR="$PWD/.verify/tmp"
              mkdir -p "$DOTNET_CLI_HOME" "$TMPDIR"
            '';
          };
        });
    };
}
