Put the real diver character FBX here:

PrototypeDiver.fbx

Unity runtime path expected by PrototypeDiverVisuals.cs:
Resources.Load<GameObject>("Characters/PrototypeDiver")

Requirements for the real model:
- Low-poly / PEAK style diver
- Orange suit variant
- Helmet + visor + headlamp
- Harness / chest clip visible
- Oxygen tank or winch pack
- Scale roughly human, Player root height ~1.7m
- No colliders/Rigidbodies needed inside the FBX; script removes them if present

Important:
This machine does not have Blender installed, so Hermes cannot export a true rigged FBX locally right now.
Import/buy/download a real model, name it PrototypeDiver.fbx, place it in this folder, then press Play.
