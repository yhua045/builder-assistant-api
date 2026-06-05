# E2E Tests

This project hosts the Playwright-based browser smoke tests for Builder Assistant API.

## Environments

The suite supports two target environments:

- `local` targets the containerized API started with `docker compose -f docker-compose.dev.yml up --build`.
- `staging` targets the real staging environment through an explicit staging base URL.

## Configuration

Set the environment with `E2E_ENVIRONMENT`.

- `E2E_ENVIRONMENT=local` defaults to `http://localhost:5001`.
- `E2E_ENVIRONMENT=staging` requires `E2E_BASE_URL` or `E2E_STAGING_BASE_URL`.
- `E2E_BASE_URL` overrides both modes when present.
- `E2E_IGNORE_HTTPS_ERRORS=true` can be used for staging endpoints with an untrusted certificate.

## Run

```bash
dotnet build tests/E2e.Tests
./tests/E2e.Tests/bin/Debug/net8.0/playwright.sh install
E2E_ENVIRONMENT=local dotnet test tests/E2e.Tests
```

For staging:

```bash
E2E_ENVIRONMENT=staging E2E_BASE_URL=https://staging.example.com dotnet test tests/E2e.Tests
```
