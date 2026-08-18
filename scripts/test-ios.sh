#!/usr/bin/env sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "${SCRIPT_DIR}/apple-common.sh"
apple_repo_root
apple_require_darwin
apple_require_project

cd "${REPO_ROOT}"
echo "No XCTest targets yet. Building iOS to verify the generated project compiles."
"${SCRIPT_DIR}/build-ios.sh"
