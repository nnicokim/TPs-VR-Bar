20 LIQUOR BOTTLES - UNITY READY (URP, Unity 6)
=============================================

HOW TO USE
1. Copy this whole "Unity_Ready" folder into your Unity project's Assets folder
   using Windows Explorer (so the .meta files come along).
2. Drag "Bottles_Unity" into the scene. Materials and textures are already assigned.

WHAT WAS FIXED
- UVs: the atlas UVs (originally on the 2nd UV channel) are now the only UV channel,
  so Unity reads the labels correctly (no more mosaic).
- Textures packed for URP/Lit and reduced from 8K to 4K:
  * Bottles_BaseMap.png           = Diffuse (RGB) + Alpha (A)
  * Bottles_MetallicSmoothness.png = Metal (RGB) + Gloss (A)
  * Bottles_Normal.png            = Normal map (OpenGL / Unity convention)
- Materials (URP/Lit):
  * Bottles_Atlas   -> opaque bottles
  * Bottles_Sprites -> printed decals on glass (alpha clipping, double sided)
- Import settings: Generate Lightmap UVs OFF (bottles should use light probes),
  no Animator component, normal map marked as Normal, data maps linear.

TIPS
- The glass look comes from reflections: put a Reflection Probe in front of the shelf and bake it.
- Bottles are small props: leave them non-static (lit by light probes / APV).

CREDITS (license CC BY-SA 4.0)
"20 Liquor bottles" by Yannick Deharo
https://sketchfab.com/3d-models/20-liquor-bottles-4729b1cfa5b74db68924190242f8ac76
Modified (UV channel fix, texture packing, Unity materials). Shared under CC BY-SA 4.0.
