# Teams calendar meeting pilot

This milestone adds an explicit **Create Teams meeting** action to the assigned tutor's session details page. It creates a Teams-enabled Outlook event, invites the booked student, and saves the join URL. Booking acceptance remains a separate step; its existing confirmation form is prefilled with the generated link. Automatic creation on acceptance and calendar cancellation/rescheduling are later milestones.

## Setup

1. Use the institutional app registration that has **Microsoft Graph → Calendars.ReadWrite → Application**, with administrator consent. Ensure any Exchange mailbox access restrictions include the pilot tutor.
2. Configure the existing `AzureAd__TenantId`, `AzureAd__ClientId`, and `AzureAd__ClientSecret` environment variables. Keep the secret in deployment configuration; do not commit it. The existing callback path is for sign-in and is not used by this app-only calendar request.
3. Apply the `AddTeamsCalendarEventMetadata` database migration through the normal deployment procedure. For example, from the repository root: `dotnet ef database update --project BC_CampusLearn`. Use the target database's usual secure configuration.
4. Set `TeamsMeetings__Enabled=true` and `TeamsMeetings__PilotTutorBcUserIds__0=<pilot tutor BcUserId>` in the application's environment. Additional pilot tutors use indices 1, 2, etc. These are CampusLearn **BcUserId** values, not TutorId or Microsoft object IDs. Both the switch and an explicit pilot account are required.
5. Use real Microsoft sign-in. In Development, set `Authentication__UseDevelopmentAuthentication=false`. Simulated development identities are deliberately rejected. The pilot tutor must have an active approved tutor profile and log in through institutional Microsoft sign-in so their encrypted tenant/object IDs are populated.

The integration uses the global Microsoft cloud at `login.microsoftonline.com` and `graph.microsoft.com`; it does not support sovereign cloud endpoints in this milestone. It uses application credentials and `/users/{tutor-object-id}/calendar/events`, not a delegated `/me` token. No `OnlineMeetings.ReadWrite.All` permission is required for this calendar approach.

## Live test

1. Arrange an upcoming **Online** test booking assigned to the allowlisted tutor, with a valid student email. Use willing test participants: creating the event sends a real invitation immediately, even if the booking is still pending.
2. Sign in as that tutor and open the booking's **Session Details** page.
3. Click **Create Teams meeting**.
4. Verify the success message and that the saved event appears in the tutor's Outlook calendar with the tutor as organizer. Check the module, scheduled start and one-hour duration. Times are sent to Graph in UTC; Outlook displays them in the user's selected time zone.
5. Verify that the student receives an invitation containing a Teams joining link. The existing confirmation form should contain the saved link. Confirm the booking normally; normal session-start/join rules still apply.
6. Retry the action or repeat its POST: a saved meeting is reused. A saved request ID is sent as Graph's `transactionId` to prevent duplicate POSTs. If the calendar event exists but its link is pending, **Retry Teams meeting** retrieves that same saved event rather than creating another.
7. Cancel the test event in the tutor's Outlook calendar after testing and clean up the test booking through CampusLearn. **This pilot does not synchronize booking cancellations or rescheduling with Outlook.**

## Troubleshooting and limits

- Missing action: check the feature switch, pilot **BcUserId**, active tutor status, future booking time, online location and absence of an existing meeting link.
- Missing Microsoft identity: sign out and sign in through the institutional provider. Existing identifiers stay encrypted in the database.
- Calendar access denied: have the administrator verify application consent, app credentials and Exchange mailbox access restrictions.
- Teams unsupported: the pilot tutor's default Outlook calendar must support `teamsForBusiness`. A manual Outlook test with a different account does not verify that tutor's access.
- A timeout or persistence error: reload and retry. The reservation is saved before calling Graph, so the same transaction is reused. Microsoft event IDs are retained once returned so links can be retrieved later. Do not manually replace a reserved/generated link.
- Do not delete a pilot booking while its meeting is being created. Deleting bookings and changing schedules are not coordinated with remote calendar events in this milestone.

Local automated tests verify request contents, permission errors, identity/tenant checks, tutor ownership, eligibility, saved links, and retry behavior using a simulated Microsoft service. They cannot verify tenant consent, licensing, actual invitations or real join links. Complete the live test above before enabling any further tutors.

References: [Create event](https://learn.microsoft.com/en-us/graph/api/calendar-post-events?view=graph-rest-1.0), [Teams-enabled calendar events](https://learn.microsoft.com/en-us/graph/outlook-calendar-online-meetings), [Application authentication](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow).
