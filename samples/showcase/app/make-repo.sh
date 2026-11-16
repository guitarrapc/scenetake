#!/usr/bin/env bash
# Builds the git history that devtools.yaml shows. Fixed names and dates make the commit hashes the same on every build.
set -euo pipefail

export GIT_AUTHOR_NAME=demo GIT_AUTHOR_EMAIL=demo@example.com
export GIT_COMMITTER_NAME=demo GIT_COMMITTER_EMAIL=demo@example.com

commit() {
  local date="$1" message="$2"
  GIT_AUTHOR_DATE="$date" GIT_COMMITTER_DATE="$date" git commit --quiet --allow-empty -m "$message"
}

git init --quiet --initial-branch=main
git add greet.py package.json
commit "2026-01-05T10:00:00Z" "Add greeting script"
commit "2026-01-06T10:00:00Z" "Add package.json scripts"
git switch --quiet -c feature/excited
commit "2026-01-07T10:00:00Z" "Add excited flag"
commit "2026-01-08T10:00:00Z" "Use dataclass for Greeting"
git switch --quiet main
commit "2026-01-09T10:00:00Z" "Fix typo in README"
GIT_AUTHOR_DATE="2026-01-10T10:00:00Z" GIT_COMMITTER_DATE="2026-01-10T10:00:00Z" \
  git merge --quiet --no-ff feature/excited -m "Merge branch 'feature/excited'"
git tag v1.2.0
git switch --quiet -c fix/output
commit "2026-01-11T10:00:00Z" "Print each greeting on its own line"
git switch --quiet main
