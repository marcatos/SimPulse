#!/usr/bin/env sh
# Shared Darwin/xcodeproj guards for Apple scripts. Sourced, not executed.

apple_require_darwin() {
  if [ "$(uname -s)" != "Darwin" ]; then
    echo "NOT EXECUTED: Apple scripts require macOS/Xcode (host is $(uname -s))."
    exit 1
  fi
}

apple_require_project() {
  if [ ! -d "${REPO_ROOT}/SimPulse.xcodeproj" ]; then
    echo "NOT EXECUTED: SimPulse.xcodeproj is missing. On a Mac: brew install xcodegen && xcodegen generate"
    exit 1
  fi
}

apple_repo_root() {
  SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
  REPO_ROOT=$(CDPATH= cd -- "${SCRIPT_DIR}/.." && pwd)
  export REPO_ROOT
}
