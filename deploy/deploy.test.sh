#!/usr/bin/env bash
# Tests deploy.sh against a temporary copy of the kleingeld folder, with midclt stubbed on PATH.
# Run: bash deploy/deploy.test.sh
set -u

here="$(cd "$(dirname "$0")" && pwd)"
failures=0
sha="0123456789abcdef0123456789abcdef01234567"

setup() {
  root="$(mktemp -d)"
  mkdir -p "$root/production" "$root/test" "$root/bin"
  cp "$here/deploy.sh" "$root/deploy.sh"
  cat > "$root/bin/midclt" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$root/midclt.log"
EOF
  chmod +x "$root/bin/midclt" "$root/deploy.sh"
}

# run "<ssh command>" "<stdin>": runs the script as sshd would for the forced command.
run() {
  out="$(printf '%s' "$2" | PATH="$root/bin:$PATH" SSH_ORIGINAL_COMMAND="$1" "$root/deploy.sh" 2>&1)"
  status=$?
}

check() {
  if eval "$2"; then echo "ok   - $1"; else echo "FAIL - $1"; echo "       output: $out"; failures=$((failures + 1)); fi
}

setup
run "deploy test $sha" "services: {}"
check "deploys to Test" '[ $status -eq 0 ]'
check "writes the compose file for Test" '[ "$(cat "$root/test/docker-compose.yaml")" = "services: {}" ]'
check "writes release.env for Test" '[ "$(cat "$root/test/release.env")" = "KLEINGELD_TAG=$sha" ]'
check "redeploys kleingeld-test" '[ "$(cat "$root/midclt.log")" = "call -job app.redeploy kleingeld-test" ]'
check "leaves Production alone" '[ -z "$(ls "$root/production")" ]'

setup
run "deploy prod $sha" "services: {}"
check "deploys to Production" '[ $status -eq 0 ]'
check "writes release.env for Production" '[ "$(cat "$root/production/release.env")" = "KLEINGELD_TAG=$sha" ]'
check "redeploys kleingeld" '[ "$(cat "$root/midclt.log")" = "call -job app.redeploy kleingeld" ]'

setup
out="$(printf 'services: {}' | PATH="$root/bin:$PATH" "$root/deploy.sh" "deploy test $sha" 2>&1)"; status=$?
check "takes the command as an argument (the sudo form)" '[ $status -eq 0 ] && [ "$(cat "$root/midclt.log")" = "call -job app.redeploy kleingeld-test" ]'

setup
out="$(printf 'services: {}' | PATH="$root/bin:$PATH" "$root/deploy.sh" deploy test "$sha" 2>&1)"; status=$?
check "refuses more than one argument" '[ $status -ne 0 ] && [ ! -e "$root/midclt.log" ]'

for bad in "" "deploy" "deploy staging $sha" "deploy test" "deploy test latest" "deploy test ${sha:0:7}" \
           "deploy test $sha extra" "deploy test $sha;rm -rf /" "rm -rf /" "deploy test ../$sha"; do
  setup
  run "$bad" "services: {}"
  check "refuses '$bad'" '[ $status -ne 0 ] && [ ! -e "$root/midclt.log" ] && [ -z "$(ls -A "$root/test")" ] && [ -z "$(ls -A "$root/production")" ]'
done

setup
run "deploy test $sha" ""
check "refuses an empty compose file" '[ $status -ne 0 ] && [ ! -e "$root/midclt.log" ] && [ ! -e "$root/test/docker-compose.yaml" ]'

setup
big="$(head -c 1048577 /dev/zero | tr '\0' 'x')"
run "deploy test $sha" "$big"
check "refuses a compose file over 1 MiB" '[ $status -ne 0 ] && [ ! -e "$root/midclt.log" ] && [ ! -e "$root/test/docker-compose.yaml" ]'

setup
cat > "$root/bin/midclt" <<'EOF'
#!/usr/bin/env bash
exit 1
EOF
run "deploy test $sha" "services: {}"
check "fails when the redeploy fails" '[ $status -ne 0 ]'

[ $failures -eq 0 ] && echo "All deploy.sh tests passed." || { echo "$failures failed."; exit 1; }
