# Mzala Connect shared Teams organiser

New meetings requested by tutor **BcUserId 2** can be created in the calendar of **mzalaconnect@belgiumcampus.ac.za**. That account remains the organiser; the assigned tutor is invited and added as **co-organiser**, and the student is invited as an attendee. Other eligible tutors retain automatic creation using their own calendars. No account password is required: the existing AzureAd tenant ID, application client ID and client secret continue to authenticate Microsoft Graph requests.

## Deploy

1. Build and deploy the updated application with the new `AddSharedTeamsOrganizer` migration. Apply it using the target database's normal migration process, for example `dotnet ef database update --project BC_CampusLearn` in an environment with the configured database connection and .NET EF tooling. The migration adds nullable account/organiser fields and a shared-account flag defaulting to false. It does not change existing organisers or links. Apply it before serving the updated application.
2. In the server's existing `.env` file, set:

   ```dotenv
   MZALA_TEAMS_ORGANIZER_OBJECT_ID=the-actual-mzalaconnect-Microsoft-Entra-object-id
   ```

3. Merge [the Compose example](teams-shared-organizer.compose.example.yml) into the existing application's `environment:` section. Keep all existing AzureAd credentials, database configuration, encryption-key storage, and uploaded-transcript volume mounts. The environment variable is passed into the container as `TeamsMeetings__SharedOrganizerObjectId`; a value in `.env` alone is not passed unless Compose references it. Recreate the app container through the normal deployment process after changing its environment.
4. Keep `SharedOrganizerPilotTutorBcUserIds__0=2` to route only the organising tutor with BcUserId 2 through the shared account. `PilotTutorBcUserIds` separately scopes whose completed meetings can be reviewed. `PilotTutorHeadBcUserIds` scopes the logged-in reviewer who can see/use the preview; set that list to the actual pilot Tutor Head's BC user ID. The example uses 2 for both only if that account performs both roles. These allowlists do not restrict automatic meeting creation for other tutors.

## Change the account through Super Admin

Open **Super Admin → Bookings and Sessions → Teams meeting account**. The form shows the email and **Microsoft Entra Object ID**, initially falling back to the environment values above. Save an account to override the environment pair for future requests. Subsequent account changes can be made here without editing code or restarting the app. Only a Super Admin can save or test these settings, including direct POST requests. Saved changes are recorded in settings audit history.

**Test calendar connection** makes a read-only calendar capability request using the values in the form; it neither saves settings nor creates/invites anyone to a meeting. It verifies calendar access and Teams support, not all artifact/co-organiser permissions. The email is a descriptive account identifier; the verified Entra Object ID determines the calendar used. A saved account overrides both environment fields together. If the shared route is enabled but its account ID is missing/invalid, the pilot request fails rather than silently falling back to the tutor's account.

## Microsoft configuration

The institution must authorise the existing application for the shared account's calendar and online meetings. Calendar creation uses `Calendars.ReadWrite` application permission. Co-organiser assignment additionally uses `OnlineMeetings.ReadWrite.All`; reading attendance and recordings uses `OnlineMeetingArtifact.Read.All` and `OnlineMeetingRecording.Read.All`. Teams-transcript downloads and Gemini transcript analysis use `OnlineMeetingTranscript.Read.All`. Gemini prefers the timestamped Teams transcript, falls back to the submitted PDF/Word transcript, and continues with the remaining review and attendance evidence if neither transcript can be read.

The Teams application access policy must contain the deployed application's client ID and be assigned to the **shared account's Microsoft Object ID**. The account must have the institution's appropriate calendar, Teams, recording and storage capabilities/licensing. Allow the policy's propagation period (Microsoft documents up to 30 minutes). Do not put the account password in these fields. Keep application client secrets in existing secure deployment configuration.

## Retries and older sessions

Each new meeting reservation stores its organiser's tenant/object identity encrypted using the app's existing Data Protection setup. That snapshot is used for creation retries, cancellation, and recording/attendance retrieval. A later account-setting change affects new reservations only. Older meetings without the snapshot keep the existing verified tutor-organiser lookup. Moving an existing meeting to a new organiser is not attempted. Keep historical organiser accounts/policies accessible while their meetings need cancellation/review; storing an ID does not prevent Microsoft from deleting or restricting those artifacts.

The event and invitation may exist before co-organiser setup finishes. CampusLearn saves the event and retries setup on that same event. It only reports success after Microsoft confirms the tutor's role; the normal confirmation path also refuses to confirm an unfinished shared-account meeting. A co-organiser error does not discard the known event or create another invitation on retry. Cancellation uses the saved organiser even after the feature is disabled or the configured account changes.

## Live pilot check

1. Use a fresh booking assigned to BcUserId 2 and willing test participants. Creation sends real calendar invitations.
2. Create Teams meeting. Verify the invite organiser is Mzala Connect, both tutor and student receive invitations, and the tutor is a co-organiser with the central account absent from the call.
3. Check the tutor can start/run/end the session and start recording as allowed by institutional policy. Recording is not automatically started by this change.
4. Complete the session and both reviews. As the configured pilot Tutor Head, verify recording playback and actual tutor/student attendance. Other Tutor Heads should keep their standard review view.
5. Cancel a separate fresh test booking. Verify the cancellation arrives from the shared account. Test a non-pilot tutor's booking to confirm their calendar path is unchanged.
6. If any stage is denied, inspect the operation and check the shared account's policy and application consent. A successful calendar test does not prove co-organiser or recording access.

References: [calendar event creation](https://learn.microsoft.com/en-us/graph/api/calendar-post-events?view=graph-rest-1.0), [online meeting updates](https://learn.microsoft.com/en-us/graph/api/onlinemeeting-update?view=graph-rest-1.0), [participant roles](https://learn.microsoft.com/en-us/graph/api/resources/meetingparticipantinfo?view=graph-rest-1.0), [Teams application access policy](https://learn.microsoft.com/en-us/graph/cloud-communication-online-meeting-application-access-policy).
