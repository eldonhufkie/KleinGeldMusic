#!/usr/bin/env bash
# Forced command for the `deploy` user's SSH key. It accepts nothing but `deploy test <sha>` or
# `deploy prod <sha>`, with the generated compose file on stdin; it writes the compose file and release.env
# into that copy's folder, next to this script, and redeploys the copy's TrueNAS custom app.
# Installing it and rolling back: docs/deploy/setup.md.
set -euo pipefail

usage() { echo "usage: deploy test|prod <40-character commit SHA>, compose file on stdin" >&2; exit 2; }

# The command arrives as the one argument when the forced command runs this through sudo
# (`sudo -n deploy.sh "$SSH_ORIGINAL_COMMAND"`, since sudo drops the environment), else from sshd directly.
[ "$#" -le 1 ] || usage
command="${1-${SSH_ORIGINAL_COMMAND:-}}"

# Word-split on spaces only; no globbing, so nothing in the command can expand.
set -f
IFS=' ' read -r -a args <<< "$command"
set +f
[ "${#args[@]}" -eq 3 ] && [ "${args[0]}" = deploy ] || usage

case "${args[1]}" in
  test) copy=test; app=kleingeld-test ;;
  prod) copy=production; app=kleingeld ;;
  *) usage ;;
esac

tag="${args[2]}"
[[ "$tag" =~ ^[0-9a-f]{40}$ ]] || usage

dir="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)/$copy"
[ -d "$dir" ] || { echo "missing $dir" >&2; exit 1; }

# A generated compose file is a few KiB; refuse anything over 1 MiB rather than deploy a cut-off file.
max=1048576
compose="$(mktemp "$dir/.docker-compose.XXXXXX")"
release="$(mktemp "$dir/.release.XXXXXX")"
trap 'rm -f "$compose" "$release"' EXIT
head -c $((max + 1)) > "$compose"
[ -s "$compose" ] || { echo "the compose file on stdin is empty" >&2; exit 1; }
[ "$(wc -c < "$compose")" -le "$max" ] || { echo "the compose file on stdin is over 1 MiB" >&2; exit 1; }
printf 'KLEINGELD_TAG=%s\n' "$tag" > "$release"
chmod 644 "$compose" "$release"

# Swap both files in only once both are complete.
mv -f "$compose" "$dir/docker-compose.yaml"
mv -f "$release" "$dir/release.env"

echo "Deploying $tag to $app"
midclt call app.redeploy "$app"
