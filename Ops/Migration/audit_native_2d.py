"""Read-only native 2D contract check. Run from any directory with Python 3.10+."""
import argparse
import collections
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
SPATIAL_TYPES = {
    54: "Rigidbody", 64: "MeshCollider", 65: "BoxCollider", 135: "SphereCollider",
    136: "CapsuleCollider", 143: "CharacterController", 57: "Joint", 59: "HingeJoint",
    138: "FixedJoint", 144: "CharacterJoint", 153: "ConfigurableJoint", 108: "Light",
    154: "TerrainCollider", 218: "Terrain", 195: "NavMeshAgent", 208: "NavMeshObstacle",
}
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+).*\n", re.M)


def documents(text):
    headers = list(HEADER.finditer(text))
    return {h.group(2): (int(h.group(1)), text[h.end():headers[i + 1].start() if i + 1 < len(headers) else len(text)])
            for i, h in enumerate(headers)}


def git_bytes(ref, relative):
    return subprocess.check_output(["git", "show", f"{ref}:{relative}"], cwd=ROOT, stderr=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compare-ref", help="Optional pre-migration commit; also verifies Scene XY and GridArt author data.")
    args = parser.parse_args()
    errors, counts = [], collections.Counter()
    paths = subprocess.check_output(["git", "ls-files", "-z", "Assets"], cwd=ROOT).decode().split("\0")
    # Include newly introduced assets without walking ignored Library projects or user plugins.
    paths += [str(p.relative_to(ROOT)).replace("\\", "/") for p in (ROOT / "Assets/_Project/Resources/Planar").rglob("*.asset")]
    scene_xy = 0
    for relative in sorted(set(paths)):
        path = ROOT / relative
        if not path.is_file() or path.suffix not in (".unity", ".prefab", ".asset"):
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            continue
        if not text.startswith("%YAML"):
            continue
        docs = documents(text)
        if re.search(r"m_Mesh: \{fileID: (?:10202|10207|10208), guid: 0000000000000000e000000000000000", text):
            errors.append(f"{relative}: retired spatial primitive mesh")
        for file_id, (type_id, body) in docs.items():
            counts[str(type_id)] += 1
            if type_id in SPATIAL_TYPES:
                errors.append(f"{relative} &{file_id}: retired {SPATIAL_TYPES[type_id]}")
            if type_id == 20 and not re.search(r"^  orthographic: 1$", body, re.M):
                errors.append(f"{relative} &{file_id}: non-orthographic camera")
        if not args.compare_ref or path.suffix != ".unity":
            continue
        before = documents(git_bytes(args.compare_ref, relative).decode("utf-8-sig").replace("\r\n", "\n"))
        for file_id, (type_id, body) in before.items():
            if type_id != 4:
                continue
            after = docs.get(file_id)
            if after is None:
                errors.append(f"{relative} &{file_id}: removed authored Transform")
                continue
            expression = r"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: [^}]+\}"
            old_xy, new_xy = re.search(expression, body), re.search(expression, after[1])
            if old_xy and new_xy and old_xy.groups() != new_xy.groups():
                errors.append(f"{relative} &{file_id}: authored XY changed")
            scene_xy += 1
    if args.compare_ref:
        # GridArt is author data, not regenerated output. Preserve bytes and stable GUIDs.
        for relative in paths:
            if relative.endswith(("Gaming_Art.asset", "Gaming_Art.asset.meta", "Gaming_Art_Ownership.asset", "Gaming_Art_Ownership.asset.meta")):
                if (ROOT / relative).read_bytes() != git_bytes(args.compare_ref, relative):
                    errors.append(f"{relative}: GridArt author data changed")
        changed = subprocess.check_output(["git", "diff", "--name-only", args.compare_ref, "--", "Assets"], cwd=ROOT, stderr=subprocess.DEVNULL).decode().splitlines()
        errors += [f"{p}: historical planning JSON changed" for p in changed if p.endswith(".json")]
    result = {"passed": not errors, "scene_transform_xy_checked": scene_xy,
              "native_collider_components": sum(counts[str(i)] for i in (58, 60, 61, 70, 197)),
              "native_bodies": counts["50"], "cameras": counts["20"], "errors": errors}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
