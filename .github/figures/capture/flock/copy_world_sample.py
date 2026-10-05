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


def mesh_sections(text):
    blocks = re.split(r"(?=^--- !u!43 &)", text, flags=re.MULTILINE)
    objects = {}
    for block in blocks[1:]:
        identifier = re.match(r"--- !u!43 &(-?\d+)", block).group(1)
        name = re.search(r"^  m_Name: (.+)$", block, re.MULTILINE).group(1)
        if name in objects:
            raise ValueError(f"Duplicate mesh name: {name}")
        objects[name] = (identifier, block)
    return blocks[0], objects


def retain_mesh_layout(text, old_objects, ids):
    header, new_objects = mesh_sections(text)
    names = list(old_objects) + [name for name in new_objects if name not in old_objects]
    blocks = [header]
    for name in names:
        if name not in new_objects:
            continue
        identifier, block = new_objects[name]
        stable_id = ids.get(identifier, identifier)
        block = re.sub(r"(^--- !u!43 &)-?\d+", lambda m: m.group(1) + stable_id, block, flags=re.MULTILINE)
        blocks.append(block)
    return "".join(blocks)


def copy_all(sample, generated):
    """Refresh all meshes, retaining GUIDs for existing assets at the same path."""
    scene_names = ("FlockSample", "FlockWorldScenarios", "FlockComparisons")
    scene_meta = {name: read(sample / (name + ".unity.meta")) for name in scene_names}
    guid_map = {}
    mesh_ids = {}
    mesh_layouts = {}
    for folder in ("Flock/Meshes", "Flock/Materials", "FlockSample/WorldMaterials"):
        source = generated / folder
        for meta in source.rglob("*.meta"):
            target = sample / source.name / meta.relative_to(source)
            if target.exists():
                guid_map[guid(meta)] = guid(target)
                if meta.name.endswith(".asset.meta"):
                    _, old_objects = mesh_sections(read(target.with_suffix("")))
                    _, new_objects = mesh_sections(read(meta.with_suffix("")))
                    ids = {identifier: old_objects[name][0] for name, (identifier, _) in new_objects.items() if name in old_objects}
                    mesh_ids[guid(meta)] = ids
                    mesh_layouts[target.with_suffix("")] = (old_objects, ids)
        target = sample / (source.name + ".meta")
        if target.exists():
            guid_map[guid(Path(str(source) + ".meta"))] = guid(target)
    for meta in (generated / "FlockSample").glob("Backdrop*.mat.meta"):
        target = sample / meta.name
        if target.exists():
            guid_map[guid(meta)] = guid(target)
    for name in ("Meshes", "WorldMeshes", "Materials", "Backdrop", "WorldMaterials"):
        target = (sample / name).resolve()
        if target.parent != sample.resolve():
            raise ValueError(f"Invalid sample target: {target}")
        if target.exists():
            shutil.rmtree(target)
        meta = sample / (name + ".meta")
        if meta.exists():
            meta.unlink()
    for name in ("Meshes", "Materials"):
        shutil.copytree(generated / "Flock" / name, sample / name)
        shutil.copy2(generated / "Flock" / (name + ".meta"), sample / (name + ".meta"))
    for name in ("WorldMaterials",):
        shutil.copytree(generated / "FlockSample" / name, sample / name)
        shutil.copy2(generated / "FlockSample" / (name + ".meta"), sample / (name + ".meta"))
    for path in (generated / "FlockSample").glob("Backdrop*.mat*"):
        shutil.copy2(path, sample / path.name)
    for name in scene_names:
        folder = "FlockComparisons" if name == "FlockComparisons" else "FlockSample"
        shutil.copy2(generated / folder / (name + ".unity"), sample / (name + ".unity"))
        (sample / (name + ".unity.meta")).write_text(scene_meta[name], encoding="utf-8", newline="\n")
    for path in sample.rglob("*"):
        if path.is_file() and path.suffix in (".asset", ".mat", ".meta", ".unity"):
            text = read(path)
            if path in mesh_layouts:
                old_objects, ids = mesh_layouts[path]
                text = retain_mesh_layout(text, old_objects, ids)
            # Unity references subassets by both GUID and fileID; retain both.
            text = re.sub(r"fileID: (-?\d+), guid: ([0-9a-f]{32})", lambda m: "fileID: "
                + mesh_ids.get(m.group(2), {}).get(m.group(1), m.group(1)) + ", guid: " + m.group(2), text)
            text = re.sub(r"guid: ([0-9a-f]{32})", lambda m: "guid: " + guid_map.get(m.group(1), m.group(1)), text)
            path.write_text(re.sub(r"[ \t]+$", "", text, flags=re.MULTILINE), encoding="utf-8", newline="\n")
    print("Updated all three scenes and meshes; preserved GUIDs of existing assets.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--all", action="store_true", help="Refresh all scenes after a vertex contract change.")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[4]
    project = args.project.resolve()
    if not project.is_relative_to(repo / "Temp"):
        parser.error("The generation project must be inside the repository's Temp directory.")

    sample = repo / "Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample"
    generated = project / "Assets/SabaProps"
    if args.all:
        copy_all(sample, generated)
        return
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
