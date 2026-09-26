#!/usr/bin/env python3
"""Refresh the world sample while preserving other scenes and shared materials."""

import argparse
from pathlib import Path
import re
import shutil


def read(path):
    return path.read_text(encoding="utf-8-sig")


def guid(path):
    match = re.search(r"^guid: ([0-9a-f]{32})$", read(path), re.MULTILINE)
    if match is None:
        raise ValueError(f"Missing asset GUID: {path}")
    return match.group(1)


def references(text):
    return set(re.findall(r"guid: ([0-9a-f]{32})", text))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[4]
    project = args.project.resolve()
    if not project.is_relative_to(repo / "Temp"):
        parser.error("The generation project must be inside the repository's Temp directory.")

    sample = repo / "Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample"
    generated = project / "Assets/SabaProps"
    scene = sample / "FlockWorldScenarios.unity"
    text = read(generated / "FlockSample/FlockWorldScenarios.unity")
    source_references = references(text)
    old_references = references(read(scene))
    preserved = references(read(sample / "FlockSample.unity"))
    preserved |= references(read(sample / "FlockComparisons.unity"))

    # The other scenes continue to use their existing shared material GUIDs.
    for material in (generated / "Flock/Materials").glob("*.mat"):
        target = sample / "Materials" / material.name
        text = text.replace(guid(Path(str(material) + ".meta")), guid(Path(str(target) + ".meta")))

    meshes = sample / "WorldMeshes"
    meshes.mkdir(exist_ok=True)
    for meta in meshes.glob("*.asset.meta"):
        meta.unlink()
        meta.with_suffix("").unlink()
    shutil.copy2(generated / "Flock/Meshes.meta", sample / "WorldMeshes.meta")
    copied = 0
    for meta in (generated / "Flock/Meshes").glob("*.asset.meta"):
        if guid(meta) in source_references:
            shutil.copy2(meta, meshes / meta.name)
            shutil.copy2(meta.with_suffix(""), meshes / meta.with_suffix("").name)
            copied += 1

    # Remove obsolete meshes only if neither of the other scenes references them.
    for meta in (sample / "Meshes").glob("*.asset.meta"):
        asset_guid = guid(meta)
        if asset_guid in old_references and asset_guid not in preserved:
            meta.unlink()
            meta.with_suffix("").unlink()

    world_materials = sample / "WorldMaterials"
    if world_materials.exists():
        shutil.rmtree(world_materials)
    shutil.copytree(generated / "FlockSample/WorldMaterials", world_materials)
    shutil.copy2(generated / "FlockSample/WorldMaterials.meta", sample / "WorldMaterials.meta")
    scene.write_text(re.sub(r"[ \t]+$", "", text, flags=re.MULTILINE), encoding="utf-8", newline="\n")
    # Preserve the distributed Scene GUID and normalise Unity's empty YAML values.
    text_assets = [sample / "WorldMeshes.meta", sample / "WorldMaterials.meta"]
    for folder in (meshes, world_materials):
        text_assets.extend(path for path in folder.rglob("*") if path.is_file())
    for path in text_assets:
        path.write_text(re.sub(r"[ \t]+$", "", read(path), flags=re.MULTILINE), encoding="utf-8", newline="\n")
    print(f"Updated world scene and {copied} meshes; preserved the other scenes and materials.")


if __name__ == "__main__":
    main()
