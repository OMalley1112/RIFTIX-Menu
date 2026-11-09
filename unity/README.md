# Desert Eagle (Unity)

The in-game Desert Eagle exported as a plain **OBJ + MTL** pair — no Unity
license or prefab step needed on either side.

- `DesertEagle.obj` — 7 cube parts (slide, barrel top, muzzle, grip, trigger
  guard, rear sight, hammer), metres, origin at the grip mount point, muzzle
  pointing +Z. Bounding box ≈ 3.3 × 16.2 × 19.4 cm.
- `DesertEagle.mtl` — flat per-part colours matching the in-game gun.
- `make_eagle_obj.py` — regenerates both files from the exact part list in
  `Mods/Admin.cs` (`BuildEagle`), so the model never drifts from the game.

Import: drag both files into a Unity project's `Assets` folder (keep them
side by side so the `.mtl` link resolves), then drag the OBJ into the scene.
Scale is already 1 unit = 1 metre.
