# Used Shaders/Variants Collector (Unity Editor Tool)

This editor utility collects all used shaders/variants in the project into a single `ShaderVariantCollection` asset.  
The collection ensures that required shader variants are precompiled and reduces runtime hitching due to on-demand shader compilation. Can definitely be improved, so feel free to change it to your liking or to fit your project/needs.

## Features
- Collects shader variants from:
  - Materials
  - Prefabs
  - Shader/Graphs
- Runs automatically before builds.
- Provides an editor window for manual collection and warmup testing.

<img width="563" height="454" alt="Shader Variant Window 2" src="https://github.com/user-attachments/assets/5860201d-a5ad-4fa5-a04d-ec256d5ee314" />

## Usage

1. Open via menu: **Jinnx → Tools → Shader Variant Collector**.
<img width="408" height="169" alt="Shader Variant" src="https://github.com/user-attachments/assets/c937177b-6543-4c2d-99c5-d957e325eca3" />


2. Set output path for the collection asset (default: `Assets/AllGameShaders.shadervariants`).

3. Click:
   - **Build Collection From Project Assets** – creates/updates the collection.
   - **Editor Warmup Test** – forces a warmup of all variants in Play Mode for hitch-testing.

## Build Integration
- The collection automatically rebuilds before each build via `IPreprocessBuildWithReport`.

## Notes
- Keep the collection path under `Assets/` so it is included in version control.
- Run **Editor Warmup Test** to confirm shader variants are available and avoid runtime compilation spikes.

