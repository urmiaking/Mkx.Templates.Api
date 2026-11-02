# Opt-in username account onboarding and recovery (Phase 7.1)

This is a backend API template with USERNAME-based authentication. Email is a verified delivery channel, not the login identifier. Registration and recovery are **disabled by default**; neither this feature nor SMTP is activated on a fresh installation.

## Enable with real credentials (environment/secret manager only)

Set these runtime environment variables before launching the API:

    AccountLifecycle__Enabled=true
    AccountLifecycle__KeyRingDirectory=/var/lib/mkx/dpkeys
    AccountLifecycle__Smtp__Host=smtp.example.com
    AccountLifecycle__Smtp__Port=587
    AccountLifecycle__Smtp__From=no-reply@example.com
    AccountLifecycle__Smtp__Username=<smtp-login>
    AccountLifecycle__Smtp__Password=<secret>

The API fails startup with incomplete settings. Port 587 is the typical STARTTLS port. SMTP delivery requires STARTTLS and authenticated SMTP; implicit TLS port 465 is not supported by this built-in transport. Do not commit any credentials. The key-ring directory must be an **absolute**, persistent, private directory writable by the app service account. In Linux containers, mount a restricted persistent volume; back it up, preserve keys across restarts, and use filesystem/volume encryption at rest. Do not expose the directory through web hosting. If multiple API replicas are deployed, they must share the SAME protected key ring and app name. Losing the key ring invalidates outstanding confirmation/recovery tokens. Restart the API to change the enabled feature state.

Identity confirmation/reset tokens expire after 20 minutes. Configuring these options has no effect when onboarding is disabled. For production, configure externally managed throttling/CAPTCHA or a verified abuse control for public email delivery; the built-in endpoint limiter is 6 requests per source IP per 5 minutes and is not an exhaustive anti-abuse solution. Make forwarded proxy IP trust explicit; never trust arbitrary client-provided X-Forwarded-For.

## Routes and security

All routes are POST, anonymous, throttled, and under /api/onboarding.

- /register: JSON Username, Email, Password, DisplayName. Creates an **unconfirmed** local Identity user. Responds 202 with no token/session, including for name/email collisions to reduce enumeration. Required username length 3-60 and password minimum length 12 with the Identity complexity rules. If SMTP fails after creation, use /resend-confirmation to recover delivery; do not send a JWT.
- /confirm: JSON Username, Token. Consumes a valid emailed Identity confirmation token, 204 on success; incorrect/expired returns 400. Local login will reject unconfirmed accounts.
- /resend-confirmation: JSON Username, Email. Generic 202 even for unknown account or incorrect email. Sends another email only for an existing unconfirmed matching account.
- /forgot-password: JSON Username, Email. Generic 202 even for unknown account. Sends code only to the verified address for a confirmed, unlocked, non-admin, non-MFA account.
- /reset-password: JSON Username, Token, NewPassword. Returns 204 after successful Identity password reset; invalid/expired returns 400. Reset updates Identity security stamp and prevents reuse of existing refresh sessions. Already-issued JWTs remain valid until their short expiration.

Never put codes in GET URLs or logs. Messages contain codes in plain email bodies; email itself is not end-to-end encrypted. Do not represent this as a phishing-resistant recovery factor. Administrator accounts cannot use anonymous password reset. Accounts with TwoFactorEnabled require password plus TOTP or one-time recovery code at login. Anonymous email-password recovery remains disabled for MFA users; see AUTHENTICATOR_MFA.md.

The SMTP sender is a replaceable Application abstraction: IAccountMessageSender. Provider-specific email or SMS integrations may implement this interface instead, but never silently log token values or bypass sender verification. Changing transport requires integration tests and explicit verification.

## Verification scope

SQLite Identity integration tests verify registration, email confirmation, mismatched email, duplicate identity, expired/invalid behavior, password reset token reuse, MFA exclusion and password update. HTTP tests verify default-disabled endpoints and invalid enablement configuration. SQL Server disposable migration/auth smoke verifies compatibility with existing features. No external mailbox service or live SMTP server has been exercised by these tests. Do not claim successful real email delivery before testing against the actual provider.

Phase 7.2 authenticator TOTP is implemented (see AUTHENTICATOR_MFA.md); SMS/email login OTP, WebAuthn/passkeys and live SMTP-provider integration remain future work.

## Bootstrap administrator on a new installation
If AccountLifecycle__Enabled=true and BootstrapAdmin__Enabled=true are both set on the initial startup, also provide BootstrapAdmin__Email=admin@example.com with a valid verified email address. This preserves unique email validation while creating the initial confirmed administrator. Do not put these values in source control. If onboarding is disabled, the older bootstrap flow remains compatible without this email option. Existing accounts are not modified or promoted.
