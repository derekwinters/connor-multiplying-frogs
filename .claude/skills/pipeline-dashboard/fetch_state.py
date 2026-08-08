#!/usr/bin/env python3
"""Print the dashboard's state snapshot as JSON, for piping into the renderer.

    python3 fetch_state.py | python3 render_dashboard.py --write

Split from the renderer on purpose. `render_dashboard.render()` is a pure
function of the state it is handed — that is what makes the golden test
possible and what stops a wrong board being anything worse than a wrong page.
Fetching is the part that needs a network, so it lives on its own.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

# The REST helper lives with the gatekeeper, which is the other caller.
_GATEKEEPER = Path(__file__).resolve().parents[1] / "pipeline-gatekeeper"
if str(_GATEKEEPER) not in sys.path:
    sys.path.insert(0, str(_GATEKEEPER))

from _github_api import _fetch_state, github_api  # noqa: E402


def main(argv=None) -> int:  # pragma: no cover - network shape
    state = _fetch_state(github_api())

    focus = os.environ.get("PIPELINE_FOCUS")
    if focus:
        state["focus"] = focus

    json.dump(state, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
