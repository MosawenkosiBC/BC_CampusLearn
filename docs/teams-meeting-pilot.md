# Teams calendar meeting pilot

Tutors confirm bookings through the **Confirm session** modal on **Session Details** or **Tutors / Sessions**. **Create Teams meeting** in the footer creates the calendar-backed Teams meeting, sends the student a calendar invitation, and confirms the booking. The page refreshes to show the confirmed status and a success modal. A grey **+** button reveals the manual meeting link input and its **Confirm session** button. Manual confirmation requires a valid, non-empty HTTP or HTTPS link; validation errors keep the modal open. Both paths apply to **online and face-to-face** sessions.

If automatic creation fails, the modal stays open with the error. The tutor can retry or enter a valid manual link and confirm. A saved creation request/event is retained for retries; a successful generated link cannot be replaced manually. After a reported failure, manual fallback is allowed, and the request metadata is retained for investigation. A timeout may mean Microsoft created an invitation even though the response was lost; retrying automatic creation reuses the same transaction. Using a manual fallback does not cancel any remote event that may already exist.

New meeting titles are **Mzala Connect tutoring session: [full module name]**, for example **Mzala Connect tutoring session: Linear Programming 281**. Module codes are omitted. Existing calendar event titles are not changed.

## Setup

1. Use the institutional app registration that has **Microsoft Graph → Calendars.ReadWrite → Application**, with administrator consent. Ensure any Exchange mailbox access restrictions include the pilot tutor.
2. Configure the existing `AzureAd__TenantId`, `AzureAd__ClientId`, and `AzureAd__ClientSecret` environment variables. Keep the secret in deployment configuration; do not commit it. The existing callback path is for sign-in and is not used by this app-only calendar request.
3. Apply all pending database migrations, including `AddTeamsCalendarEventMetadata`, `AddTeamsManualFallback`, and `AddTeamsMeetingCancellation`, through the normal deployment procedure. For example, from the repository root: `dotnet ef database update --project BC_CampusLearn`. Use the target database's usual secure configuration. The cancellation migration adds durable request, last-attempt and completion timestamps to the meeting-link table. No existing links are removed.
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

## Cancellation synchronization

Cancelling a booking as the tutor or booked student saves its cancellation request in the same database transaction as the booking change, then immediately calls Microsoft's organizer-calendar `event/cancel` action. This sends a cancellation notice to attendees and removes the scheduled event. Pending bookings that already have a generated event are covered, including a tutor's decline. Automatic booking expiry/cancellation also queues remote cleanup.

If Microsoft is unavailable, the booking remains cancelled and the actor sees that Teams cancellation is pending. A background worker processes the durable queue every minute, at most five bookings per pass. Timestamp concurrency protects retry state and reduces concurrent processing; completed requests are skipped. Missing/already-cancelled events are treated idempotently. Retries run even if the pilot feature is later disabled or a tutor becomes inactive.

If a create response was lost before its event ID could be saved, cancellation searches the organizer's calendar around the scheduled date for the exact saved transaction ID. It does not recreate a cancelled event or guess by title. Unresolved requests remain pending for retry/investigation. This search cannot recover an event that was manually rescheduled outside that date range.

Only events created by Mzala Connect can be cancelled through this integration. The organizer must cancel any manually supplied external meeting. Cancelling a calendar event is not a guarantee that a previously distributed Teams join URL is immediately invalidated; this feature cancels the scheduled event and its invitations, not an ongoing call or a standalone meeting through a different API. Rescheduling synchronization remains a later milestone.

Reference: [Microsoft Graph event cancellation](https://learn.microsoft.com/en-us/graph/api/event-cancel?view=graph-rest-1.0).

## Live test

1. Arrange upcoming **Online** and **face-to-face** test bookings assigned to the allowlisted tutor, with valid student emails. Use willing test participants: creation sends a real invitation.
2. Sign in as that tutor and open a booking's **Session Details** page. Choose **Confirmed** in the status control and save to open its confirmation modal. Also test the **Confirm session** action from **Tutors / Sessions**.
3. Verify that the manual link is initially hidden. Click **Create Teams meeting** in the footer. Verify that the booking becomes confirmed, the meeting link is saved, and a success modal reports that the invitation was sent and the session confirmed. Dismiss the modal and verify the confirmed status remains after refreshing. For another pending booking, click **+** and submit an empty or invalid manual link: the modal must remain open with a validation error and no meeting should be created. Enter a valid link and confirm: the booking must become confirmed and the modal close. Both requests show progress and block repeat clicks while running. A failed or still-preparing meeting must leave the booking pending without showing the success modal; retry should reuse the same meeting.
4. Verify the success message and that the saved event appears in the tutor's Outlook calendar with the tutor as organizer. Check the module, scheduled start and one-hour duration. Times are sent to Graph in UTC; Outlook displays them in the user's selected time zone.
5. Verify that the student receives an invitation containing a Teams joining link, with the new Mzala Connect title and full module name. The booking should be confirmed, with its saved join link. Face-to-face bookings retain their physical location; normal session-start/join rules still apply.
6. Test a reported failure with a controlled test setup. Check that the booking remains pending and the modal stays open. Retry with the link blank to retrieve/reuse the same meeting request, or paste a valid manual URL and confirm. A saved request ID is sent as Graph's `transactionId` to prevent duplicate POSTs. A saved event with a pending link is retrieved rather than recreated.
7. Cancel one generated booking as the tutor and another as the booked student. Verify the calendar-backed Teams event is cancelled and the student receives a cancellation notice. Also cancel/decline a pending booking after generating its link. Under a controlled Microsoft outage, verify the app reports pending synchronization and the worker completes it after connectivity is restored. Manually cancel any external fallback meetings used during the test.

## Tutor Head recording and attendance review

The Tutor Head session details page now shows reviews beside a Teams recording card. The recording card contains one player per recording part, actual video duration after loading the player, meeting start/end and duration from Teams attendance, and tutor/student join/leave intervals and total attendance. Times are displayed in SAST. Reconnecting produces separate attendance intervals; overlapping intervals from multiple devices are merged to avoid counting the same time twice. Participants are matched by institutional email, never by display name. An unmatched/missing record is unavailable evidence, not proof of absence.

The existing `TeamsMeetings__Enabled` switch and `PilotTutorBcUserIds` allowlist also gate this read integration. It only reads saved application-created meetings, verifies the stored organizer identity, and requires a completed booking with both evaluations. Manually submitted recording links remain available as external links. They are never fetched with application credentials. The Tutor Head role is required for the page and its recording/transcript handlers. Video bytes are streamed through the authenticated application, including single byte-range requests, without exposing Microsoft tokens or buffering the recording in memory. Nothing is downloaded until the player is loaded. Responses are private and not cached.

Submitted transcript files must also survive deployments. They are stored under `App_Data/tutor-review-transcripts` relative to the application's content root; the database stores their metadata, not the file contents. Mount persistent storage at that directory in the container, preserve existing uploaded files when introducing the mount, and ensure all application replicas see the same files. Do not assume that persisting Data Protection keys also persists transcript uploads. A transcript is not consumed or deleted by viewing it. If its button produces a not-found response after deployment, verify the uploaded file and its stored metadata on the server. The Tutor Head handler now explains missing-file failures, prevents caching them, and accepts stored path separators from Windows or Linux. Already lost files require restoration from backup or a supported re-upload process.

In addition to the existing calendar consent, the application needs administrator consent for **OnlineMeetings.Read.All**, **OnlineMeetingArtifact.Read.All**, **OnlineMeetingRecording.Read.All**, and **OnlineMeetingTranscript.Read.All**. A Teams application access policy containing the application's client ID must be assigned to the pilot tutor's Microsoft object ID. Consent alone does not establish this policy. Keep recording, transcription, and attendance enabled through institutional Teams policies. Changes to the application access policy can take up to 30 minutes to propagate. These permissions read artifacts that exist; they do not start recording or transcription.

**Generate AI assessment** (or **Regenerate AI assessment** for a saved result) adds suggested transcript time ranges and reasons to the AI result modal, not the review form. If exactly one Teams transcript exists, generation retrieves its timestamped VTT; otherwise it uses the tutor's uploaded transcript. The transcript remains accessible through buttons. Actual Teams meeting timing is supplied separately to the AI and displayed separately from transcript duration. Suggestions with invalid intervals are removed; text transcript suggestions are additionally bounded by the available timestamps. A PDF's timestamps are interpreted by the AI and require human verification. Suggestions are transcript-relative and currently displayed as text, without automatic video seeking: recording/transcript offsets and multiple parts have not been verified against real pilot artifacts. No transcription/video alignment is assumed.

Unavailable/expired artifacts or permission failures leave reviews and manually submitted evidence usable. Attendance, recording, and transcript errors are shown separately. If Teams returns multiple attendance reports for a reused meeting, attendance is withheld rather than choosing an arbitrary occurrence. Each recording part can still be opened separately. The current implementation retrieves metadata on page load; it does not persist an attendance snapshot and therefore remains subject to Microsoft's retention policy. No database migration is required.

### Live verification for this milestone

1. Confirm the additional Graph consents and the pilot tutor's application access policy. Use an application-created test booking and willing test participants.
2. Complete a meeting with recording and transcription enabled, submit both evaluations, and open it as a Tutor Head. Verify the player works and seeking works through byte-range responses. Confirm video duration against the recording itself and meeting duration against the Teams attendance report.
3. Have one participant disconnect/rejoin. Verify intervals, total attendance, and SAST timestamps. Test institutional identities and an anonymous guest; the guest must not be assigned to a booked user by display name.
4. Generate an AI assessment. Confirm the overview and suggested ranges appear only in the AI result modal, and that both existing uploaded-transcript and Teams-transcript buttons work. Regenerate a previously saved assessment to obtain the new suggestions.
5. Test missing artifacts, denied artifact permission, and expired/deleted recordings. Reviews, transcript fallback, and the submitted recording link must remain usable. Check narrow/mobile layouts: cards stack and the review form remains usable.
6. As a non-Tutor Head or signed-out user, verify recording/transcript handlers cannot be read. A recording ID from another meeting must also be rejected.

References: [Get online meeting](https://learn.microsoft.com/en-us/graph/api/onlinemeeting-get?view=graph-rest-1.0), [attendance reports](https://learn.microsoft.com/en-us/graph/api/meetingattendancereport-get?view=graph-rest-1.0), [recording content](https://learn.microsoft.com/en-us/graph/api/callrecording-get?view=graph-rest-1.0), [transcript content](https://learn.microsoft.com/en-us/graph/api/calltranscript-get?view=graph-rest-1.0), [application access policy](https://learn.microsoft.com/en-us/graph/cloud-communication-online-meeting-application-access-policy).

## Meeting creation troubleshooting and limits

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
