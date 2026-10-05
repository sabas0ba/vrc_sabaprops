let
  dotfiles = builtins.getFlake "/workspace";
  expectedRevision = "597283ad8aa0b331c788e97c4c262d58877074ef";
  pkgs = import dotfiles.inputs.nixpkgs { system = "x86_64-linux"; };
in
assert dotfiles.inputs.nixpkgs.rev == expectedRevision;
pkgs.buildEnv {
  name = "sabaprops-verification-tools";
  paths = with pkgs; [
    dotnet-sdk_8
    glslang
    python3
    curl
    unzip
  ];
}
