#!/usr/bin/env bash
set -euo pipefail

# run-migrations.sh
# Usage: ./run-migrations.sh "<connection-string>"
# Example: ./run-migrations.sh "Server=db;Database=BuilderAssistantDb;User Id=sa;Password=Your_password123;"

CONN="${1:-}"
if [ -z "$CONN" ]; then
  echo "Usage: $0 '<connection-string>'"
  exit 1
fi

export ConnectionStrings__DefaultConnection="$CONN"

MAX_ATTEMPTS=60
ATTEMPT=0

until dotnet ef database update --project src/Infrastructure --startup-project src/Api; do
  ATTEMPT=$((ATTEMPT+1))
  echo "Waiting for database to be ready... attempt $ATTEMPT/$MAX_ATTEMPTS"
  if [ "$ATTEMPT" -ge "$MAX_ATTEMPTS" ]; then
    echo "Timed out waiting for the database."
    exit 1
  fi
  sleep 2
done

echo "Migrations applied."
