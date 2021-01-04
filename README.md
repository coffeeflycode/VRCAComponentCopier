# VRCAComponentCopier
A tool for unity to quickly copy components from one unity object to another. Specifically designed for use for avatars on vrchat.

Tool is located under VRChat SDK menu for convenience.
Usage is simple, add an object from the Hierarchy in unity to copy from, and then add another object to copy to, and fire away.

- Copies all components except for:
  - SkinnedMeshRenderers 
  - Transforms 
  - Animators   
- Copies position and scale of the root of the old object to new object.
- Updates references for:
  - **VRC Avatar Descriptor:**
    - SkinnedMeshRenderers
    - Left/Right Eye Bones
  - **Dynamic Bone:**
    - Update Root
    - Colliders
    - Exclusions
    - Reference Object

![image](https://user-images.githubusercontent.com/76971405/103578909-65d59a80-4ea5-11eb-90d7-2b68cf20c744.png)

