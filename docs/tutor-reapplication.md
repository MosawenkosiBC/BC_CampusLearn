# Returning tutor applications

Deregistered tutors may submit updated application inputs and documents when tutor applications are open. CampusLearn reuses their existing `TutorId` (the application/profile ID) and `BcUserId`; historical bookings and their module relationships are retained.

During review, `Status` is Pending, `IsReapplication` is true, `IsActive` is false, and the user retains the Student role. The usual submission, shortlist and interview stages apply. A second submission while review is pending is blocked.

Rejection at any stage, including bulk rejection, restores Deregistered status and sends the usual unsuccessful notification. Returning tutors are excluded from rejected-profile deletion. Their updated application inputs remain on the same record; rejection does not restore the previous inputs. A deregistered tutor can submit again while applications are open, including the same open window.

Placement restores Approved status and the Tutor role. Only the newly submitted module selections become active. Historical module rows remain present and inactive unless selected again. Availability and leadership roles are not restored. Previous application documents are archived and omitted from the current application review; replacement documents start unverified.

Every successful deregistration appends a `TutorDeregistrationEvents` row containing its date, administrator ID/name and reason. Existing latest-deregistration columns remain for compatibility. Administrator → Tutors → Profile displays the complete recorded deregistration history, including after reactivation.

## Deployment

Apply migration `20261021133059_AddTutorReapplicationAndDeregistrationHistory` before serving the updated application, using the target database's normal migration process, for example `dotnet ef database update --project BC_CampusLearn`.

The migration copies every existing non-null `Tutors.DeregisteredAt` record into the new history table. Earlier events that were already overwritten before this change cannot be reconstructed from those columns. No migration has been applied to a deployed database as part of this implementation.
