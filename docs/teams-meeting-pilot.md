# Teams calendar meeting pilot

Tutors confirm bookings through the **Confirm session** modal on **Session Details** or **Tutors / Sessions**. Leaving the optional manual link blank creates a Teams-enabled Outlook event, invites the booked student, saves the join URL, and confirms the booking. This applies to **online and face-to-face** sessions. The separate Teams action at the top of Session Details has been removed. Calendar cancellation/rescheduling remain later milestones.

If automatic creation fails, the modal stays open with the error. The tutor can retry or enter a valid manual link and confirm. A saved creation request/event is retained for retries; a successful generated link cannot be replaced manually. After a reported failure, manual fallback is allowed, and the request metadata is retained for investigation. A timeout may mean Microsoft created an invitation even though the response was lost; retrying automatic creation reuses the same transaction. Using a manual fallback does not cancel any remote event that may already exist.

New meeting titles are **Mzala Connect tutoring session: [full module name]**, for example **Mzala Connect tutoring session: Linear Programming 281**. Module codes are omitted. Existing calendar event titles are not changed.

## Setup

1. Use the institutional app registration that has **Microsoft Graph → Calendars.ReadWrite → Application**, with administrator consent. Ensure any Exchange mailbox access restrictions include the pilot tutor.
2. Configure the existing `AzureAd__TenantId`, `AzureAd__ClientId`, and `AzureAd__ClientSecret` environment variables. Keep the secret in deployment configuration; do not commit it. The existing callback path is for sign-in and is not used by this app-only calendar request.
3. Apply all pending database migrations, including `AddTeamsCalendarEventMetadata` and `AddTeamsManualFallback`, through the normal deployment procedure. For example, from the repository root: `dotnet ef database update --project BC_CampusLearn`. Use the target database's usual secure configuration. The fallback migration adds a flag to the existing meeting-link table; no existing links are removed.
4. Set `TeamsMeetings__Enabled=true` and `TeamsMeetings__PilotTutorBcUserIds__0=<pilot tutor BcUserId>` in the application's environment. Additional pilot tutors use indices 1, 2, etc. These are CampusLearn **BcUserId** values, not TutorId or Microsoft object IDs. Both the switch and an explicit pilot account are required.
5. Use real Microsoft sign-in. In Development, set `Authentication__UseDevelopmentAuthentication=false`. Simulated development identities are deliberately rejected. The pilot tutor must have an active approved tutor profile and log in through institutional Microsoft sign-in so their encrypted tenant/object IDs are populated.

The integration uses the global Microsoft cloud at `login.microsoftonline.com` and `graph.microsoft.com`; it does not support sovereign cloud endpoints in this milestone. It uses application credentials and `/users/{tutor-object-id}/calendar/events`, not a delegated `/me` token. No `OnlineMeetings.ReadWrite.All` permission is required for this calendar approach.

## Preserve encryption keys across Docker deployments

CampusLearn's stored Microsoft identifiers use ASP.NET Core Data Protection. `IdentityProtection__LookupKey` is a separate stable identity-fingerprint key; it does **not** decrypt those identifiers. Keep its existing value unchanged.

Merge the following into your existing Compose configuration, retaining all existing environment settings and volume mounts:

```yaml
services:
  app:
    environment:
      DataProtection__KeysPath: "/var/lib/campuslearn/data-protection"
    volumes:
      - campuslearn_keys:/var/lib/campuslearn/data-protection

volumes:
  campuslearn_keys:
```

The `volumes:` inside `app` is alongside `environment:`, not inside it. The final `volumes:` is at the root of the Compose file; add `campuslearn_keys` to your existing root volume declarations. Ensure the application's container user can write to this directory. Keep the named volume across rebuilds and restrict access to its encryption keys.

If the current key files are still available, preserve them in the mounted directory before switching locations. Keys protect sign-in cookies and other encrypted application data as well as Microsoft identifiers. Existing sign-in cookies may stop working if the key ring changes. If the old keys have already been lost, users must sign in through Microsoft again: CampusLearn can repair their encrypted Microsoft identifiers only after the verified tenant/object fingerprint matches the existing account. This preserves their BC user ID and tutor profile; it does not recover other encrypted secrets whose keys were lost.

After deploying with persistent key storage, have the pilot tutor sign out, sign back in through Microsoft, and retry automatic creation through **Confirm session**. The identity recovery fix itself requires no migration; the confirmation fallback change requires `AddTeamsManualFallback`.

Reference: [Microsoft's Docker key-persistence guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0#persisting-keys-when-hosting-in-a-docker-container).

## Live test

1. Arrange upcoming **Online** and **face-to-face** test bookings assigned to the allowlisted tutor, with valid student emails. Use willing test participants: creation sends a real invitation.
2. Sign in as that tutor and open a booking's **Session Details** page. Choose **Confirmed** in the status control and save to open its confirmation modal. Also test the **Confirm session** action from **Tutors / Sessions**.
3. Leave the optional manual link blank and click **Confirm session**. The modal shows progress and prevents repeat submissions while the request runs.
4. Verify the success message and that the saved event appears in the tutor's Outlook calendar with the tutor as organizer. Check the module, scheduled start and one-hour duration. Times are sent to Graph in UTC; Outlook displays them in the user's selected time zone.
5. Verify that the student receives an invitation containing a Teams joining link, with the new Mzala Connect title and full module name. The booking should be confirmed, with its saved join link. Face-to-face bookings retain their physical location; normal session-start/join rules still apply.
6. Test a reported failure with a controlled test setup. Check that the booking remains pending and the modal stays open. Retry with the link blank to retrieve/reuse the same meeting request, or paste a valid manual URL and confirm. A saved request ID is sent as Graph's `transactionId` to prevent duplicate POSTs. A saved event with a pending link is retrieved rather than recreated.
7. Cancel the test event in the tutor's Outlook calendar after testing and clean up the test booking through CampusLearn. **This pilot does not synchronize booking cancellations or rescheduling with Outlook.**

## Troubleshooting and limits

- Missing automatic-creation text: check the feature switch and pilot **BcUserId**. Eligibility requires an active tutor and an upcoming pending booking. The automatic flow applies to both physical and online bookings.
- Missing Microsoft identity: sign out and sign in through the institutional provider. Existing identifiers stay encrypted in the database.
- Stored Microsoft sign-in details cannot be read: deploy the recovery fix, configure persistent key storage above, and sign in again. Do not delete the BC user record or change the identity lookup key to resolve this error.
- Calendar access denied: have the administrator verify application consent, app credentials and Exchange mailbox access restrictions.
- Application authentication failed: the UI now includes a safe Microsoft/MSAL error reference. Check `AADSTS7000215` for an invalid secret (use the client secret **value**, not its ID), `AADSTS7000222` for an expired secret, and `AADSTS700016` for an application/tenant mismatch. A network error does not establish that credentials are wrong. Only bounded error codes, HTTP status and a valid correlation ID are logged; exception messages, response bodies and secrets are not logged. After reproducing the error, inspect those safe lines with `docker compose logs --tail=200 app | grep 'Microsoft Graph application authentication failed'`. Share the error reference with the administrator, not your `.env` or client secret. See [Microsoft's client-secret troubleshooting](https://learn.microsoft.com/en-us/entra/msidweb/authentication/client-secrets).
- Teams unsupported: the pilot tutor's default Outlook calendar must support `teamsForBusiness`. A manual Outlook test with a different account does not verify that tutor's access.
- A timeout or persistence error: follow the displayed error. Reload if the confirmation outcome is unknown. Saved request IDs and event IDs are retained for retries. After a reported Teams failure, a valid manual link can be used; successfully generated links remain protected from replacement.
- Do not delete a pilot booking while its meeting is being created. Deleting bookings and changing schedules are not coordinated with remote calendar events in this milestone.

Local automated tests verify request contents, permission errors, identity/tenant checks, tutor ownership, eligibility, saved links, and retry behavior using a simulated Microsoft service. They cannot verify tenant consent, licensing, actual invitations or real join links. Complete the live test above before enabling any further tutors.

References: [Create event](https://learn.microsoft.com/en-us/graph/api/calendar-post-events?view=graph-rest-1.0), [Teams-enabled calendar events](https://learn.microsoft.com/en-us/graph/outlook-calendar-online-meetings), [Application authentication](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow).
