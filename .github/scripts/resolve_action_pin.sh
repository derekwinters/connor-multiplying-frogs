#!/usr/bin/env bash
#
# Resolve a GitHub Action tag to the full commit SHA to pin it at, and print the
# line to paste into a workflow.
#
#     .github/scripts/resolve_action_pin.sh actions/checkout v7.0.1
#     → actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
#
# Why a script rather than "just look at the tag": an *annotated* tag's ref
# points at a tag object, not at a commit, and `git ls-remote refs/tags/v1.2.3`
# hands you that tag-object SHA. Pasting it into a workflow gives you a pin that
# resolves to nothing. The peeled ref — `refs/tags/v1.2.3^{}` — is the commit,
# and this checks for it first, falling back to the plain ref for lightweight
# tags that have no peeled form.
#
# See docs/engineering/ci-cd.md.

set -euo pipefail

if [ $# -ne 2 ]; then
    echo "usage: $(basename "$0") <owner/action> <tag>" >&2
    echo "example: $(basename "$0") actions/checkout v7.0.1" >&2
    exit 2
fi

repo=$1
tag=$2
url="https://github.com/${repo}"

# Peeled ref first: for an annotated tag this is the commit it points at.
sha=$(git ls-remote "$url" "refs/tags/${tag}^{}" | cut -f1)

if [ -z "$sha" ]; then
    # Lightweight tag: the ref is the commit.
    sha=$(git ls-remote "$url" "refs/tags/${tag}" | cut -f1)
fi

if [ -z "$sha" ]; then
    echo "no tag '${tag}' in ${repo}" >&2
    exit 1
fi

if [ ${#sha} -ne 40 ]; then
    echo "resolved '${sha}' for ${repo}@${tag}, which is not a 40-character SHA" >&2
    exit 1
fi

echo "${repo}@${sha} # ${tag}"
