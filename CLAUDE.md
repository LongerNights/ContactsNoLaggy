# ContactsNoLaggy

*Auto-loaded project guide for Claude sessions in this folder. Keep it short and current.*

A Resonite mod (RML) that removes the freeze the **first time the Contacts dash tab is opened** after the game
starts. Keep it as simple as possible. Repo: https://github.com/LongerNights/ContactsNoLaggy (branch `main`);
commit/push only when asked.

- Resonite install: `E:\Program Files (x86)\Steam\steamapps\common\Resonite` (MonkeyLoader + RML compat, mods in
  `rml_mods\`).
- Decompiler: `ilspycmd` (`~/.dotnet/tools`). Decompiled engine code goes in `ref/`, which is git-ignored: never
  commit it (Resonite's mod policy bars redistributing engine code).

## How it works

- **Cause (dotnet-trace):** ~95% of the 8-10 s freeze is `Canvas.FinishCanvasUpdate -> SubmitGraphics ->
  GraphicsChunk.RenderData.UpdateMaterials -> GetMaterialMap -> CloneMaterial -> Slot.DuplicateComponent(s) ->
  Slot.GenerateHierarchy`. Inside a mask (scroll area) each chunk clones its materials; the shared UI materials sit on
  the userspace **Root** slot (`World.GetSharedComponentOrCreate` attaches to `RootSlot`), so every clone walked all
  ~28k userspace slots. Building the ~600 rows themselves (`ContactsDialog.OnAttach`) is only ~600 ms.
- **Fix (`src/Mod.cs`), three patches:**
  1. While `CloneMaterial` runs (thread-static flag), `Slot.GenerateHierarchy` adds only the slot it was called on.
     Safe: that set only classifies refs as internal; with `preserveMissingTargets: true` uncopied targets are kept
     either way, and CloneMaterial drives the clone's fields from the source.
  2. Prebuild: once `Contacts.ContactListLoaded`, call the hidden Contacts wrapper's `OnShow` (hidden screens have an
     inactive ScreenRoot, so the canvas does no work).
  3. Throttle `UpdateContactItem` row *creation* to 2 ms/frame, **only while the dialog is inactive**. On a visible
     canvas each added row forces a relayout, so trickling rows in is much worse than stock.
- Results (614 contacts): stock 8-10 s freeze; clone fix alone 728 ms worst frame; plus prebuild 519 ms (what's left
  is about 400 ms of material clones at first draw, which can't be prebuilt). Dead end: deferring the sort.

## Build and test

- `dotnet build src -c Release` (references the install's DLLs; override with `-p:ResonitePath=...`).
- `tools/redeploy.ps1`: gracefully closes Resonite (never kills it), builds with `-p:InstallMod=true`, relaunches
  with `-Screen`. Ask the user before running it.
- Measure freezes with a wall-clock Stopwatch: `Time.Delta` is clamped to 100 ms.
- Profiling: `dotnet-trace collect -p <Renderite.Host pid> --profile dotnet-sampled-thread-time --duration ...`
  (the engine runs in **Renderite.Host**, not Resonite.exe). Let the duration expire: killing the trace leaves an
  unreadable file. Read it with the TraceEvent library, filtering by thread and UTC time window.
- McpLink `eval` is not installed here; use `logs` (`pattern`), `find_components` (`typePattern`), `du`, `reflect_get`.

# Resonite / McpLink
@CLAUDE-MCPLINK.md
