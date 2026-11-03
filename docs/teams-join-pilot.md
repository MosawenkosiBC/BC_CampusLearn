# Teams application join pilot — BcUserId 2 only

When approved tutor **BcUserId 2** joins their booked meeting directly in Teams, Mzala Connect can change the booking from Confirmed to In Progress. The restriction uses BcUserId, not TutorId, and is fixed in code. Existing TeamsMeetings pilot lists do not control this feature. Other tutors retain their current flow.

## Configuration and certificate

The public endpoint is `https://tutors.belgiumcampus.ac.za/api/teams/meeting-events` (POST). Deploy the code before Graph can validate that URL. It must be reachable without interactive sign-in or a reverse-proxy login. Graph validation returns its supplied token as plain text; actual notifications require Microsoft signature, audience, tenant and publisher verification, a per-booking HMAC client state, and encrypted payload verification.

Generate the RSA 3072-bit self-signed encryption certificate and random secrets using .NET 10:

```powershell
dotnet run --project tools/TeamsJoinPilotSetup -- "$PWD/.local-secrets/teams-join-pilot"
```

The generator refuses to overwrite existing files and restricts directory access to the current Windows user and SYSTEM (or owner-only on Unix). Output is ignored by Git:

- `teams-join-notifications.pfx`: password-protected private key, valid for one year.
- `teams-join-notifications.cer`: public certificate only.
- `local-settings.json`: local settings for .NET user secrets; contains secrets.
- `deployment.env`: environment settings for the hosted app; contains secrets.

This certificate is used only to decrypt Graph notifications. Do not install it as a trusted root or replace the website HTTPS certificate. Graph receives only its public half. The app uses an ephemeral private key loaded from the PFX; OS certificate-store installation is unnecessary.

For local development, import the generated settings without printing them:

```powershell
Get-Content .local-secrets/teams-join-pilot/local-settings.json -Raw | dotnet user-secrets set --project BC_CampusLearn
```

Local execution with the production NotificationUrl does not receive production callbacks. Run only the deployed subscription worker for the live pilot; override `TeamsJoinPilot__Enabled=false` locally until using a separate test webhook/app registration.

For the hosted deployment, securely copy the PFX and deployment.env to the server; use the existing secret-management process. Merge [the Compose example](teams-join-pilot.compose.example.yml) into the existing service. The example mounts the PFX read-only at `/run/secrets/teams-join-notifications.pfx`. Grant the app's runtime user read access on the host; do not make the private key world-readable. For IIS, use an absolute Windows path outside wwwroot and grant only the application-pool identity read access. Configure the equivalent environment variables from deployment.env with that Windows path. Restart the app after configuration changes.

The checked-in settings enable the pilot but leave secrets blank. Without all required settings it remains inactive, logs an actionable startup warning, and returns 404 on the webhook. Invalid/expired certificates prevent subscriptions; they never bypass validation. The app does not expose a public diagnostics endpoint.

## Subscriptions and start behavior

The existing app registration needs admin-consented **Application** `OnlineMeetings.Read.All` (or `OnlineMeetings.ReadWrite.All`). Existing permissions for calendar creation and co-organiser setup still apply separately.

Every minute, a worker lists Graph subscriptions (including continuation pages), creates subscriptions for confirmed Teams bookings for user 2 within the next 24 hours, and renews its subscriptions before expiry. Subscriptions last 50 minutes, avoiding long-subscription lifecycle requirements. Graph is the subscription registry, so restarting the app does not create duplicates. Graph redacts clientState on list/get; a non-secret namespace derived from this feature's key is stored in encryptionCertificateId to identify owned subscriptions for renewal/deletion. Notification authentication still verifies the actual secret clientState independently. Keep the key stable between restarts and across app instances. Run one subscription worker instance for this pilot to avoid competing reconciliation passes; duplicate deliveries cannot restart an already running booking.

The webhook accepts only fresh roster events for the assigned tutor's saved Microsoft tenant and object identity, with `isInLobby=false` and no removal state. Student joins, shared-organiser joins, anonymous joins, lobby waits, leaves, mismatched meetings, and other tutors are ignored. Both notification event time and receipt time must fall in the existing window: from 5 minutes before until strictly before 15 minutes after scheduled start. Events older than 2 minutes are ignored. Joining earlier and staying connected does not produce a new join event at the window boundary; use the existing manual start in that case.

The existing lifecycle service starts the normal countdown, creates the student notification and writes system-attributed history with reason `TutorJoinedTeams`. SessionExecution.StartSource is `TeamsApplication` (3). SQL row versions protect races with manual starts, cancellation and automatic expiry; failed concurrent writes are retried through webhook delivery. No schema migration is required. Current countdown/completion and manual start behavior remain unchanged.

Requests are bounded to 256 KiB. Notifications are processed before acknowledgement rather than accepted into an in-memory queue: transient failures return 503 for Graph retry. Invalid notifications are discarded. Keep response times low; Graph delivery is asynchronous and can be delayed or missed, so manual start remains the fallback. The pilot does not reverse an automatic cancellation when a join arrives late. Application logs record booking IDs and operation/failure types, never payloads, private keys, tokens or meeting URLs.

## Verify after deployment

1. Confirm the certificate and environment settings exist in the deployed app and only one worker instance is running.
2. Create/confirm a fresh Teams booking assigned to BcUserId 2. Wait for the log `Created Teams join subscription for pilot booking ...` (up to one minute). Do this before the tutor joins.
3. Join directly from Teams using user 2's linked college Microsoft account inside the start window. Refresh Mzala Connect and verify In Progress, one SessionExecution with StartSource 3, one start notification and system history `TutorJoinedTeams`.
4. Rejoin the meeting and verify the countdown has not reset. Test student/shared-account joins, a tutor outside the pilot, lobby waiting, and joins outside the start window: none should start the session.
5. If subscription creation fails, check HTTP status, application consent, public endpoint access, reverse-proxy rules and PFX access. A trusted-root installation is not a fix for Graph subscription permissions.

To stop the pilot, set `TeamsJoinPilot__Enabled=false` and restart. The webhook immediately rejects events and no renewals occur; outstanding subscriptions expire within 50 minutes. Keep the manual buttons available.

Before certificate expiry, generate a new bundle in a new protected directory. Stop the pilot, wait 50 minutes for existing subscriptions to expire, replace certificate/secrets and re-enable it. This pilot intentionally does not implement zero-downtime certificate rotation. Never commit the private output or copy it under wwwroot.

References: [meeting call events](https://learn.microsoft.com/en-us/graph/changenotifications-for-onlinemeeting), [rich notification validation and encryption](https://learn.microsoft.com/en-us/graph/change-notifications-with-resource-data), [webhook delivery](https://learn.microsoft.com/en-us/graph/change-notifications-delivery-webhooks).
