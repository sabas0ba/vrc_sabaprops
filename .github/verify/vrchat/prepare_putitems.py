"""Assemble the Put Items SDK test project from SHA256-verified local archives."""
from pathlib import Path
import hashlib
import shutil
import zipfile

REPO = Path(__file__).resolve().parents[3]
PROJECT = REPO / "build" / "PutItemsProject"
PACKAGE = "io.github.sabas0ba.sabaprops.putitems"


def replace(source: Path, target: Path) -> None:
    if not target.resolve().is_relative_to(PROJECT.resolve()):
        raise ValueError(f"Target outside test project: {target}")
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(source, target)


def main() -> None:
    for folder in ("Assets", "Packages", "ProjectSettings"):
        (PROJECT / folder).mkdir(parents=True, exist_ok=True)
    lock = REPO / ".github/verify/vrchat/packages.lock"
    for line in lock.read_text().splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        name, version, digest, _ = line.split()
        archive = REPO / "build/vpm" / f"{name}-{version}.zip"
        if hashlib.sha256(archive.read_bytes()).hexdigest() != digest:
            raise ValueError(f"SDK hash mismatch: {archive}")
        target = PROJECT / "Packages" / name
        if target.exists():
            shutil.rmtree(target)
        with zipfile.ZipFile(archive) as source:
            for entry in source.infolist():
                if not (target / entry.filename).resolve().is_relative_to(target.resolve()):
                    raise ValueError(f"Archive path outside package: {entry.filename}")
            source.extractall(target)
        print(f"Verified and extracted {name} {version}", flush=True)
    replace(REPO / "Packages" / PACKAGE, PROJECT / "Packages" / PACKAGE)
    replace(REPO / "Packages" / PACKAGE / "Tests~", PROJECT / "Assets/PutItemsTests")
    shutil.copy2(REPO / ".github/verify/vrchat/manifest.json", PROJECT / "Packages/manifest.json")
    shutil.copy2(REPO / ".github/verify/CIProject/ProjectSettings/ProjectVersion.txt",
                 PROJECT / "ProjectSettings/ProjectVersion.txt")
    # SDK 3.10.4 has a scene template whose file and class names disagree.
    template = PROJECT / "Packages/com.vrchat.worlds/Editor/VRCSDK/SDK3/VRCDefaultWorldScene.scenetemplate"
    template.unlink(missing_ok=True)
    template.with_suffix(template.suffix + ".meta").unlink(missing_ok=True)
    print(f"Assembled {PROJECT}", flush=True)


if __name__ == "__main__":
    main()
