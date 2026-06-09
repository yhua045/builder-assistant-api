#!/usr/bin/env bash
set -euo pipefail

# Runs the E2E Playwright tests and writes a TRX result file into TestResults/
# Usage: ./scripts/run-e2e-with-trx.sh [dotnet test args]

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT_DIR"

echo "Installing Playwright browsers (if needed)..."
if [ -f ./tests/E2e.Tests/bin/Debug/net8.0/playwright.sh ]; then
  ./tests/E2e.Tests/bin/Debug/net8.0/playwright.sh install || true
fi

echo "Running E2E tests and producing TRX..."
dotnet test tests/E2e.Tests --logger "trx;LogFileName=E2E.trx" --results-directory "TestResults" "$@"

echo "TRX written to TestResults/ (if tests ran)."
