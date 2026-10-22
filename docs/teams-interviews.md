# Teams interviews in admin applications

Apply `20261022072351_AddTutorInterviewMeetings` before running this version. It adds durable interview meeting requests and widens the existing interview link field; it does not remove application data.

From the repository root:

```powershell
dotnet ef database update --project BC_CampusLearn
```

The feature reuses `TeamsMeetings:Enabled`, the existing Microsoft Graph application credentials and `TeamsOrganizerSettingsStore`. Configure the Mzala Connect account under the existing Teams meeting account settings. The account needs an Exchange calendar that supports Teams and the existing `Calendars.ReadWrite` application permission. This flow does not assign co-organisers or need additional online-meeting permissions for that purpose. Development sign-in cannot send invitations; use the institutional Microsoft sign-in flow.

On Shortlist → View, the applicant and schedule occupy equal columns on desktop and stack on small screens. The applicant is always invited as required. The signed-in administrator appears in the panel by default with a “You” label and can be removed. Staff search and manual email entry support up to 20 panel members, each required or optional. Internal preparation notes never enter the calendar invitation.

Creating reserves an immutable payload and transaction ID before calling Microsoft. The applicant only moves to Interview after a valid Teams URL is saved. A failed or ambiguous create retains the application in Shortlist; the modal retains the form and retries the saved request under its original organiser. Once reserved, the schedule is locked until creation finishes to avoid silently creating a different invitation. After completion, Edit interview can change the title, date, time, duration and panel. The original invitation message and Teams join block are retained. Pending updates retry the same event and saved payload. Times entered in the form are South Africa Standard Time (UTC+02:00); Graph receives UTC.

Rejected, deleted or replaced applications retain enough meeting metadata for the existing cancellation worker to cancel their invitation. Cancellation retries do not create new meetings. Regular tutor-booking co-organiser behavior is unchanged; this interview flow never calls co-organiser assignment.

Verification uses fake Graph responses, including failures and retries, so automated tests do not send email or create real meetings. End-to-end delivery still needs a deliberately scheduled interview using the configured Microsoft account.
