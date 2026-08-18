#!/usr/bin/env sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck disable=SC1091
. "${SCRIPT_DIR}/apple-common.sh"
apple_repo_root
apple_require_darwin
apple_require_project

cd "${REPO_ROOT}"
DEST=${SIMPULSE_IOS_DESTINATION:-"platform=iOS Simulator,name=iPhone 17"}
echo "Testing SimPulse iOS for ${DEST}"
xcodebuild -project SimPulse.xcodeproj -scheme SimPulse -destination "${DEST}" -configuration Debug CODE_SIGNING_ALLOWED=NO CODE_SIGN_IDENTITY=- test
