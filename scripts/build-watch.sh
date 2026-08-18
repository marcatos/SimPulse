#!/usr/bin/env sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "${SCRIPT_DIR}/apple-common.sh"
apple_repo_root
apple_require_darwin
apple_require_project

cd "${REPO_ROOT}"
DEST=${SIMPULSE_WATCH_DESTINATION:-"generic/platform=watchOS Simulator"}
echo "Building SimPulse Watch for ${DEST}"
xcodebuild -project SimPulse.xcodeproj -scheme SimPulseWatch -destination "${DEST}" -configuration Debug CODE_SIGNING_ALLOWED=NO build
