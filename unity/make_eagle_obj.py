"""Emit the in-game Desert Eagle as an OBJ + MTL pair for Unity.

Geometry mirrors AddEaglePart calls in Mods/Admin.cs: every part is a unit
cube given a local position, a scale, an optional rotation and a flat colour.
Root sits at the origin, units are metres, so it drops straight into a Unity
project's Assets folder.
"""

import math
import os

# localPosition, scale, colour, localRotation euler (degrees, XYZ)
PARTS = [
    ("Slide",      (0.0, -0.005, 0.095), (0.030, 0.042, 0.150), (0.10, 0.10, 0.11), (0.0, 0.0, 0.0)),
    ("BarrelTop",  (0.0,  0.026, 0.095), (0.033, 0.024, 0.155), (0.58, 0.58, 0.62), (0.0, 0.0, 0.0)),
    ("Muzzle",     (0.0,  0.005, 0.180), (0.018, 0.020, 0.030), (0.04, 0.04, 0.05), (0.0, 0.0, 0.0)),
    ("Grip",       (0.0, -0.062, 0.030), (0.026, 0.090, 0.040), (0.06, 0.06, 0.07), (12.0, 0.0, 0.0)),
    ("TriggerGuard",(0.0, -0.030, 0.078), (0.024, 0.008, 0.050), (0.08, 0.08, 0.09), (0.0, 0.0, 0.0)),
    ("RearSight",  (0.0,  0.045, 0.160), (0.006, 0.014, 0.008), (0.70, 0.70, 0.72), (0.0, 0.0, 0.0)),
    ("Hammer",     (0.0,  0.042, 0.030), (0.020, 0.012, 0.012), (0.70, 0.70, 0.72), (0.0, 0.0, 0.0)),
]

# unit cube corners (v is applied per-vertex before scale/rotate/translate)
CORNERS = [
    (-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.5, 0.5, -0.5), (-0.5, 0.5, -0.5),
    (-0.5, -0.5,  0.5), (0.5, -0.5,  0.5), (0.5, 0.5,  0.5), (-0.5, 0.5,  0.5),
]

# outward-facing, counter-clockwise when seen from outside
FACES = [
    (2, 3, 7, 6), (1, 5, 8, 4), (4, 8, 7, 3),
    (1, 2, 6, 5), (5, 6, 7, 8), (1, 4, 3, 2),
]


def rot_xyz(euler_deg):
    """Unity-style XYZ euler (Z*X*Y apply order: here X, then Y, then Z extrinsic
    for these all-axis-aligned parts - only the grip uses a single X tilt)."""
    rx, ry, rz = (math.radians(a) for a in euler_deg)

    cx, sx = math.cos(rx), math.sin(rx)
    cy, sy = math.cos(ry), math.sin(ry)
    cz, sz = math.cos(rz), math.sin(rz)

    # R = Rz * Ry * Rx (matches Unity Quaternion.Euler for these small tilts)
    return (
        (cz * cy, cz * sy * sx - sz * cx, cz * sy * cx + sz * sx),
        (sz * cy, sz * sy * sx + cz * cx, sz * sy * cx - cz * sx),
        (-sy,     cy * sx,                cy * cx),
    )


def transform(corner, scale, mat, pos):
    x = corner[0] * scale[0]
    y = corner[1] * scale[1]
    z = corner[2] * scale[2]
    return (
        mat[0][0] * x + mat[0][1] * y + mat[0][2] * z + pos[0],
        mat[1][0] * x + mat[1][1] * y + mat[1][2] * z + pos[1],
        mat[2][0] * x + mat[2][1] * y + mat[2][2] * z + pos[2],
    )


def main():
    out_dir = os.path.dirname(os.path.abspath(__file__))
    obj_path = os.path.join(out_dir, "DesertEagle.obj")
    mtl_path = os.path.join(out_dir, "DesertEagle.mtl")

    colours = []
    for part in PARTS:
        if part[3] not in colours:
            colours.append(part[3])

    mtl = ["# Desert Eagle materials - RIFTIX gun colours"]
    for i, colour in enumerate(colours):
        mtl.append("newmtl EagleMat%d" % i)
        mtl.append("Kd %.4f %.4f %.4f" % colour)
        mtl.append("Ka 0.0000 0.0000 0.0000")
        mtl.append("Ks 0.0500 0.0500 0.0500")
        mtl.append("Ns 24.0000")
        mtl.append("")

    obj = [
        "# Desert Eagle - built from RIFTIX Mods/Admin.cs BuildEagle",
        "# 7 cube parts, metres, origin at the grip mount point",
        "mtllib DesertEagle.mtl",
        "",
    ]

    vert_index = 1
    all_verts = []

    for name, pos, scale, colour, euler in PARTS:
        mat = rot_xyz(euler)
        obj.append("o %s" % name)
        obj.append("usemtl EagleMat%d" % colours.index(colour))

        for corner in CORNERS:
            v = transform(corner, scale, mat, pos)
            obj.append("v %.6f %.6f %.6f" % v)
            all_verts.append(v)

        for face in FACES:
            obj.append("f " + " ".join(str(vert_index + i - 1) for i in face))

        vert_index += 8
        obj.append("")

    with open(mtl_path, "w", encoding="utf-8") as f:
        f.write("\n".join(mtl) + "\n")
    with open(obj_path, "w", encoding="utf-8") as f:
        f.write("\n".join(obj) + "\n")

    xs = [v[0] for v in all_verts]
    ys = [v[1] for v in all_verts]
    zs = [v[2] for v in all_verts]
    print("wrote", obj_path)
    print("wrote", mtl_path)
    print("parts:", len(PARTS), "verts:", len(all_verts))
    print("bbox x[%.3f, %.3f] y[%.3f, %.3f] z[%.3f, %.3f]" % (
        min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
    print("size %.1f x %.1f x %.1f cm" % (
        (max(xs) - min(xs)) * 100, (max(ys) - min(ys)) * 100, (max(zs) - min(zs)) * 100))


if __name__ == "__main__":
    main()
