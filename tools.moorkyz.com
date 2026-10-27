#!/bin/sh
# POSIX launcher for the project-agnostic Node.js CLI.
exec node "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)/src/.moorkyz/launchers/tools.moorkyz.com.cjs" "$@"