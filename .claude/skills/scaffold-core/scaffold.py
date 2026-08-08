#!/usr/bin/env python3
"""Create a new Core type and its failing test.

Run this **before** writing any logic. It leaves you at the *start* of the red
phase: an empty class, and a test that fails.

    python3 .claude/skills/scaffold-core/scaffold.py Pond
    python3 .claude/skills/scaffold-core/scaffold.py Pond --subfolder Rules

What it writes, following docs/engineering/tech-stack.md:

    Assets/Scripts/Core/<sub>/<Name>.cs        the type, in Frogs.Core[.<Sub>]
    Assets/Scripts/Core/<sub>/<Name>.cs.meta   a pinned GUID, so Unity does not
                                               invent one on first import
    Tests/Core/<sub>/<Name>Tests.cs            a test that fails

Stdlib only.
"""

from __future__ import annotations

import argparse
import re
import sys
import uuid
from pathlib import Path

CORE_ROOT = "Assets/Scripts/Core"
TEST_ROOT = "Tests/Core"
CORE_NAMESPACE = "Frogs.Core"
TEST_NAMESPACE = "Frogs.Core.Tests"

TYPE_NAME = re.compile(r"^[A-Z][A-Za-z0-9]*$")


def validate_type_name(name: str) -> str:
    """A PascalCase C# identifier, or an error.

    Deliberately does not "fix" a lowercase name: a scaffolder that silently
    corrects what you asked for produces a file you then cannot find.
    """
    trimmed = (name or "").strip()

    if not TYPE_NAME.match(trimmed):
        raise ValueError(
            f"'{name}' is not a usable type name. Use PascalCase with no dots or "
            "spaces — `Pond`, `FrogColony`, `SplitRule`."
        )

    return trimmed


def namespace_for(subfolder: str) -> str:
    parts = [part for part in (subfolder or "").replace("\\", "/").split("/") if part]
    return ".".join([CORE_NAMESPACE] + parts)


def test_namespace_for(subfolder: str) -> str:
    parts = [part for part in (subfolder or "").replace("\\", "/").split("/") if part]
    return ".".join([TEST_NAMESPACE] + parts)


def new_guid() -> str:
    return uuid.uuid4().hex


def meta_file(guid: str) -> str:
    """The .meta for a C# file.

    Modelled on the shape Unity writes for a script asset. The `guid` is the
    part that matters — it is the asset's identity, and everything that
    references the file references this rather than its path. See
    docs/engineering/unity-serialization.md.
    """
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "MonoImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )


def class_source(name: str, namespace: str) -> str:
    """An empty type.

    Empty on purpose. A class arriving with a plausible implementation already
    in it is an invitation to skip writing the test that should have driven it,
    which is the one thing this skill exists to prevent.
    """
    return (
        f"namespace {namespace}\n"
        "{\n"
        f"    public sealed class {name}\n"
        "    {\n"
        "    }\n"
        "}\n"
    )


def test_source(name: str, namespace: str, test_namespace: str) -> str:
    """A test that fails.

    Not an empty stub: running the suite straight after scaffolding must go
    red, so the loop starts where it is supposed to. The failure message says
    what to do next.
    """
    return (
        "using NUnit.Framework;\n"
        f"using {namespace};\n"
        "\n"
        f"namespace {test_namespace}\n"
        "{\n"
        f"    public sealed class {name}Tests\n"
        "    {\n"
        "        [Test]\n"
        f"        public void {name}_DoesTheThingTheIssueAskedFor()\n"
        "        {\n"
        f"            // Replace this with one real behaviour of {name}, named for\n"
        "            // the behaviour. Run it, watch it fail, and check it failed\n"
        "            // for the reason you meant — then write the smallest code\n"
        "            // that passes it.\n"
        f"            Assert.Fail(\"Write the first real test for {name}.\");\n"
        "        }\n"
        "    }\n"
        "}\n"
    )


def scaffold(root: Path, name: str, subfolder: str = "") -> list[Path]:
    """Write the three files. Returns their paths."""
    name = validate_type_name(name)
    relative = (subfolder or "").strip("/")

    class_path = root / CORE_ROOT / relative / f"{name}.cs"
    meta_path = class_path.with_name(f"{name}.cs.meta")
    # Tests/Core is outside Assets/, so Unity never sees it — a .meta there
    # would be a file nothing reads.
    test_path = root / TEST_ROOT / relative / f"{name}Tests.cs"

    for path in (class_path, meta_path, test_path):
        if path.exists():
            raise FileExistsError(
                f"{path} already exists. Scaffolding over it would discard whatever "
                "is in it; delete it first if that is what you want."
            )

    class_path.parent.mkdir(parents=True, exist_ok=True)
    test_path.parent.mkdir(parents=True, exist_ok=True)

    class_path.write_text(class_source(name, namespace_for(relative)), encoding="utf-8")
    meta_path.write_text(meta_file(new_guid()), encoding="utf-8")
    test_path.write_text(
        test_source(name, namespace_for(relative), test_namespace_for(relative)),
        encoding="utf-8",
    )

    return [class_path, meta_path, test_path]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("name", help="the type name, PascalCase")
    parser.add_argument("--subfolder", default="", help="under Assets/Scripts/Core/, e.g. Rules")
    parser.add_argument("--root", default=".", help="the repository root")
    arguments = parser.parse_args(argv)

    try:
        written = scaffold(Path(arguments.root).resolve(), arguments.name, arguments.subfolder)
    except (ValueError, FileExistsError) as error:
        print(error, file=sys.stderr)
        return 1

    root = Path(arguments.root).resolve()
    for path in written:
        print(f"  {path.relative_to(root)}")

    print(
        "\nNow run the suite. It should be RED:\n"
        "  dotnet test Tests/Core/Frogs.Core.Tests.csproj"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
