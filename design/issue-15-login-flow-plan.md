# Design Plan: Login Flow & Internal OAuth Server (Issue #15)

## 1. Overview
The goal is to evolve the authentication system toward a standard OAuth 2.0 authorization code flow with PKCE, while maintaining a simple UX boundary for the frontend. The backend will act as an internal identity provider.

In accordance with the constraints:
- The frontend will handle all UI login logic.
- The backend will use `/login` and `/verify-otp` to issue an **Authentication Cookie**, proving identity.
- Once authenticated, the frontend transitions into the standard OAuth PKCE flow via `/authorize` and `/connect/token`.

## 2. API Endpoints

### A. Authentication Sequence
The entry points that establish user identity and create the session cookie.

**`POST /api/auth/login`**
- **Purpose**: Initiates the login flow (password or passwordless). The backend determines the correct flow at runtime based on the user's configured authentication method.
- **Payload**: `{ "email": "user@example.com", "password": "optional_password" }` (Note: `email` is always required to identify the user and flow).
- **Behavior**:
  - **Password Flow**: If the user requires a password, it evaluates the `password` field. If valid, creates an HTTP-Only Authentication Cookie to establish the session. Returns `{ "next": "/authorize" }`.
  - **Passwordless Flow**: If the user relies on passwordless authentication (or no password is provided), generates an OTP, dispatches it to the user, and returns `{ "next": "/verify-otp" }`.

**`POST /api/auth/verify-otp`**
- **Purpose**: Validates the OTP for passwordless login.
- **Payload**: `{ "email": "user@example.com", "otp": "123456" }`
- **Behavior**: If the OTP is valid, creates the HTTP-Only Authentication Cookie. Returns `{ "next": "/authorize" }`.

### B. OAuth PKCE Sequence
The standard endpoints that map the active session cookie into OAuth tokens.

**`GET /api/auth/authorize`**
- **Authorization**: Requires the **Authentication Cookie** (returns 401 or redirects to frontend login if missing).
- **Query Params**: `client_id`, `redirect_uri`, `response_type=code`, `code_challenge`, `code_challenge_method=S256`, `state`.
- **Behavior**: Validates the request, generates a short-lived authorization `code` mapped to the user and PKCE `code_challenge`.
- **Response**: `302 Redirect` to `redirect_uri` appending `?code={code}&state={state}`.

**`POST /api/auth/connect/token`**
- **Purpose**: Exchanges the authorization code for application tokens.
- **Payload**: `grant_type=authorization_code`, `client_id`, `code`, `redirect_uri`, `code_verifier`
- **Behavior**: Validates the `code` and ensures the SHA256 hash of the `code_verifier` matches the previously stored `code_challenge`.
- **Response**: `{ "access_token": "jwt...", "refresh_token": "...", "expires_in": 3600, "token_type": "Bearer" }`

**`POST /api/auth/logout`**
- **Purpose**: Terminates the session (clears the HTTP-only cookie and invalidates tokens).

## 3. Storage and State Models
- **OTP Codes**: Short TTL (e.g., 5-10 mins) stored against the user's email/ID in cache/DB. **Important:** Generating a new OTP explicitly invalidates any previously generated, unused OTPs (only the most recently generated OTP remains valid).
- **Authorization Codes**: Short TTL (e.g., 1-5 mins) containing the `UserId`, `ClientId`, `RedirectUri`, and `CodeChallenge`. Let's store this in cache or DB.
- **Tokens**: JWT for access token (stateless verification). Refresh tokens stored with a long TTL in the DB for rotation/revocation.

## 4. Frontend Integration Contract
1. Frontend captures email/password -> `POST /login`.
2. Evaluates the response:
   - If `next == /verify-otp`, frontend prompts for OTP -> `POST /verify-otp` -> evaluates response.
   - If `next == /authorize`, the backend has set the Auth Cookie.
3. Frontend triggers PKCE generation (Code Verifier & Code Challenge).
4. Frontend redirects the user (or follows through) to `GET /authorize?code_challenge=...`
5. Frontend receives the `code` via the `redirect_uri`.
6. Frontend executes `POST /connect/token` to get the final JWT.

## 5. Implementation Phases
1. **Domain & Application Layer**: Set up models for OTP and Authorization Codes tracking.
2. **Setup Cookie Authentication**: Create an `Identity.Application` cookie scheme distinct from the `Bearer` scheme used by the APIs.
3. **Authentication Controllers**: Implement `/login` and `/verify-otp`.
4. **OAuth Controllers**: Implement `/authorize` and `/connect/token` supporting PKCE logic.
