# Email and platform notifications

CampusLearn sends transactional email through Microsoft Graph with the same
application credentials used by the Teams integration. Email is always sent
from `mzalaconnect@belgiumcampus.ac.za` by posting to:

`POST https://graph.microsoft.com/v1.0/users/mzalaconnect@belgiumcampus.ac.za/sendMail`

The application token comes from `AzureAd__TenantId`, `AzureAd__ClientId`, and
`AzureAd__ClientSecret`. Do not configure SMTP for booking notifications and do
not put Microsoft credentials in source control.

## Deployment

1. Apply the `AddEmailNotificationSystem` database migration.
2. Set `EmailNotifications__Enabled=true`.
3. Keep `EmailNotifications__SenderMailbox` set to
   `mzalaconnect@belgiumcampus.ac.za`. Other sender mailboxes are intentionally
   rejected by the application and by the institution's Exchange policy.
4. Set `EmailNotifications__PublicBaseUrl` to the public HTTPS origin of the
   deployed site, for example `https://campuslearn.example/`. Email still sends
   without this value, but asks the recipient to sign in instead of including a
   direct link.
5. Confirm that the existing app registration has its institution-approved
   `Mail.Send` application access. No separate app registration is used.

Development email delivery is disabled in `appsettings.Development.json`.

## Delivery behavior

Business actions save an email-outbox row in the same database transaction as
the platform notification. A background worker sends pending rows in batches.
Temporary failures, including an initial HTTP 403 while Exchange policy changes
propagate, are retried with capped backoff. Malformed requests and missing
mailboxes are marked as permanent failures for investigation. A failed email
does not reverse a booking, review, role change, nomination, or other completed
business action.

Graph tokens and raw Microsoft response bodies are never written to logs.

## Booking wording

When a student submits the booking form, both the success message and the
student notification explicitly say that the request is **pending** and that
the tutor must confirm it before the session is booked. The tutor receives a
separate request asking them to confirm or decline it.

## Optional mailbox auto-reply

The Microsoft 365 administrator can configure an automatic reply on the Mzala
Connect mailbox stating that it is not monitored. Every generated email also
contains a short do-not-reply notice.
