# Unity serialization

**Never guess.** This is the page
[`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
rule #6 points at.

## Why guessing here is different

In most of this codebase, a wrong guess fails: the compiler rejects it, or a
test goes red. Unity's asset files do not work that way.

Unity's YAML deserializer **silently ignores keys it doesn't recognise.** Write
`m_SpriteRenderer` where the format says `m_Renderer`, and nothing complains.
The file parses. The editor opens. The asset loads — with that field left at its
default, because as far as Unity is concerned you never set it.

What you get is:

- no error at import;
- no error at build;
- an object that is subtly wrong at runtime, usually as "the sprite is missing"
  or "the reference is null" three screens into the game.

The same applies to enum values. These fields are serialized as integers, and an
out-of-range integer is not an error — it is a different behaviour, or a
silently clamped one.

So the failure mode isn't "it broke". It's "it worked, then didn't, and the
change that caused it was four PRs ago". That is the most expensive kind of bug
there is, and it is entirely avoidable by not guessing.

## The rule

**Before hand-authoring any Unity asset file, verify every key name and enum
value against a real file that Unity itself wrote.**

Not against memory, not against a tutorial, not by pattern-matching from a
similar field. Against a file in this project, or one Unity generated on demand.

### How to verify

1. **Find an existing example.** Grep the project for the key:

   ```bash
   rg 'm_SortingOrder' Assets/ ProjectSettings/
   ```

2. **If there isn't one, make Unity write it.** Create the thing in the editor —
   set the field to a distinctive value — save, and read the file. Ten minutes
   in the editor beats a silent failure in a fortnight.

3. **Diff before and after.** Change one field in the editor, save, and
   `git diff`. That diff is the authoritative answer to "what does this field
   look like when set", including the keys you didn't know existed.

4. **Prefer having Unity write it at all.** Hand-authoring is a last resort, for
   when a headless environment has no editor. If the editor is available, use
   it, and commit what it produced.

### What this means for an agent with no editor

Agent environments have no Unity. So:

- **Copy the shape from a real file in the repo**, changing only values you can
  see the effect of.
- **Never invent a key.** If the shape you need isn't in the repo, say so, and
  open a `Direct Involvement Needed` issue for the editor step rather than
  guessing at the YAML.
- **Say what you copied from** in the PR — "modelled on
  `Assets/Prefabs/Frog.prefab`" — so a reviewer can check the source rather than
  re-deriving the format.

## The file shapes

### `.meta` — every asset has one

```yaml
fileFormatVersion: 2
guid: 9f2c1b7a4e5d4c8fa1b6d3e0c7a58412
TextureImporter:
  serializedVersion: 13
  spriteMode: 1
  spritePixelsToUnits: 100
  filterMode: 0
  textureType: 8
  spriteSheet:
    sprites: []
```

Things worth knowing:

- **`guid` is the asset's identity.** Everything that references the asset
  references this GUID, not the path. Delete the `.meta` and Unity generates a
  new GUID on re-import — every reference to that asset breaks at once. This is
  why `.meta` files are committed
  ([see `.gitignore`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/.gitignore)).
- **`serializedVersion` matters.** The meaning of the keys below it depends on
  it. Copying a block from a file with a different `serializedVersion` is
  exactly the guess this page exists to prevent.
- **The importer block name is type-specific** — `TextureImporter`,
  `AudioImporter`, `ModelImporter`, `MonoImporter`. Wrong block name, silently
  ignored block.
- **Enum values are integers.** `textureType: 8` is Sprite. There is no
  in-file hint of that, and the numbering differs per enum. Verify against a
  real file.

### A component in a scene or prefab

```yaml
--- !u!114 &1234567890123456789
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 987654321098765432}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: 3a7f0e21c4b94d8e9f6a2c5b8d1e4073, type: 3}
  m_Name:
  _pausePanelWidth: 280
  _frogPrefab: {fileID: 4728391056274839201, guid: 6b1d9f3e8c2a4571b0e7d4c9a3f28516, type: 3}
```

- **`!u!114`** is the class ID — 114 is `MonoBehaviour`. The number after `&` is
  the local file ID, unique within this file.
- **`m_Script`** identifies which script, by the GUID of its `.cs` file's
  `.meta`. A wrong GUID here is the classic "the component shows as *Missing
  Script*" — and note it is not an error, just a missing component.
- **Serialized private fields keep their C# names**, including the underscore:
  `_pausePanelWidth`, not `pausePanelWidth` and not `m_PausePanelWidth`. Renaming
  the field in C# without `[FormerlySerializedAs]` silently loses the value.
- **References are `{fileID, guid, type}` triples.** `type: 3` means the target
  lives in another asset file; `type: 2` is a script. A reference within the
  same file is `{fileID: …}` alone.

### `ProjectSettings/*.asset`

Same YAML, same rules, one difference in consequence: a wrong key here is wrong
for the whole project, and often only visible in a build rather than in the
editor. `ProjectSettings.asset` in particular is where the version, bundle
identifier, and Android settings live — all touched by CI, none of them things
to hand-edit casually. See [Versioning](versioning.md).

## Pinning GUIDs

Anything referenced by GUID has to keep that GUID forever. Two rules follow.

### Never regenerate a GUID

- **Don't delete a `.meta` file.** Not to "clean up", not to resolve a conflict.
  Deleting it and letting Unity re-import assigns a new GUID and detaches every
  reference — the "all my prefabs lost their sprites" failure.
- **Move assets with the `.meta`.** `git mv` the pair together. Unity follows
  the GUID, so a moved asset with its `.meta` keeps all its references; a moved
  asset without one is a new asset.
- **Resolve `.meta` conflicts by keeping one side's GUID**, never by taking a
  merge that produces a third. If a conflict has produced two GUIDs for one
  asset, keep the one already referenced elsewhere.

### Sprite internal IDs, too

A sliced sprite sheet is one asset containing many sprites, and each sprite has
an **internal ID** inside the sheet's `.meta`:

```yaml
    spriteSheet:
      sprites:
        - name: frog_idle_0
          internalID: 21300000
        - name: frog_idle_1
          internalID: 21300002
      internalIDToNameTable:
        - first: {213: 21300000}
          second: frog_idle_0
```

References to an individual sprite use `{fileID: 21300000, guid: <sheet guid>}`.
Re-slicing a sheet can renumber those internal IDs, which breaks every reference
to individual frames while leaving the sheet's own GUID intact — so the asset
looks fine and the references don't.

So: **when re-slicing, preserve `internalID` and the `internalIDToNameTable`
entries**, and check the diff for renumbering before committing. A re-slice that
renumbers is a re-slice that needs every reference updated in the same PR.

## Guard the wiring with EditMode tests

Everything above is a class of failure that is invisible until it isn't. The
defence is a test that asserts the wiring, at the serialization level, and fails
loudly.

These are EditMode tests — they need the editor, so they run in CI
([Testing](testing.md)) — and they are cheap:

```csharp
[Test]
public void FrogPrefab_HasItsSpriteAndScriptWiredUp()
{
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Frog.prefab");
    Assert.That(prefab, Is.Not.Null, "prefab missing or its GUID changed");

    var view = prefab.GetComponent<FrogView>();
    Assert.That(view, Is.Not.Null, "FrogView missing — usually a broken m_Script GUID");
    Assert.That(view.Sprite, Is.Not.Null, "sprite reference lost — check the sheet's internalIDs");
}
```

### What to guard

Write one of these for anything where a silent detachment would cost real
debugging time:

- **Every prefab that a scene depends on** — that it loads, and that its
  components are present rather than *Missing Script*.
- **Every serialized reference that must not be null** — sprites, prefabs,
  audio clips, scriptable objects.
- **Sprite lookups by name**, so a re-slice that renumbers internal IDs fails a
  test rather than a screen.
- **Any hand-authored asset file**, always. If it was written by hand, it gets a
  test — that is the deal that makes hand-authoring acceptable at all.

### Assert on the failure, not the value

`Is.Not.Null` with a message naming the likely cause is worth more than an
assertion on a specific value. The failure being guarded against is *detachment*,
not incorrectness, and the message is what turns a red test into a two-minute
fix instead of an afternoon.
