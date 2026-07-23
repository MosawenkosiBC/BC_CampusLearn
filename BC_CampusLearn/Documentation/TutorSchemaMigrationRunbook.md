# Tutor schema migration runbook

Do not run `Update-Database` until the migration and production data have been
reviewed and backed up.

## Stage 1

Review and apply `AddUserAndTutorDocumentStructure`. It creates `BcUsers` and
`TutorDocuments`, and adds nullable application/profile columns to `Tutors`.
The legacy tutor identity, name, email, and approval columns remain intact.

## Required backfill

1. Export and validate the current `Tutors` rows and take a restorable backup.
2. Identify rows whose legacy Entra values are absent, duplicated, or cannot be
   converted with `TRY_CONVERT(uniqueidentifier, ...)`.
3. Obtain each tutor's personnel number from the approved Belgium Campus source.
   Do not derive or invent personnel numbers.
4. Insert one `BcUsers` row per distinct `(EntraTenantId, EntraObjectId)` pair,
   using converted GUID values and the verified personnel number.
5. Set `Tutors.BcUserId` by matching the legacy tenant/object pair to `BcUsers`.
6. Populate `ProgrammeId`, `OverallAverage`, `YearOfStudy`,
   `ReasonForTutoring`, `TeachingStyle`, and `DemonstrationVideoUrl` from
   verified application data.
7. Set `Status = 1` where legacy `IsApproved = 1`; otherwise set `Status = 0`
   unless a reviewed status is known.
8. Populate `SubmittedAt` and `CreatedAt` with verified timestamps, or an
   explicitly approved UTC fallback.
9. Verify that every tutor has exactly one linked user, required application
   fields are populated, years are 1–4, averages are 0–100, personnel numbers
   are unique, Entra pairs are unique, and every biography is at most 500
   characters.

Example validation queries:

```sql
SELECT TutorId, EntraObjectId, EntraTenantId
FROM Tutors
WHERE EntraObjectId IS NULL
   OR EntraTenantId IS NULL
   OR TRY_CONVERT(uniqueidentifier, EntraObjectId) IS NULL
   OR TRY_CONVERT(uniqueidentifier, EntraTenantId) IS NULL;

SELECT TutorId, LEN(Biography) AS BiographyLength
FROM Tutors
WHERE LEN(Biography) > 500;

SELECT TutorId
FROM Tutors
WHERE BcUserId IS NULL
   OR ProgrammeId IS NULL
   OR OverallAverage IS NULL
   OR YearOfStudy IS NULL
   OR ReasonForTutoring IS NULL
   OR TeachingStyle IS NULL
   OR DemonstrationVideoUrl IS NULL;
```

## Stage 2 plan

Only after the backfill and validation pass, generate
`FinalizeTutorAndBcUserStructure` from the final runtime model. Review the
generated migration and add explicit SQL to convert `IsApproved` to `Status`
before dropping legacy columns. The migration must:

1. Make the three BC identity fields and all required tutor application fields
   non-nullable.
2. enforce unique personnel numbers, unique Entra tenant/object pairs, and a
   unique `Tutors.BcUserId`;
3. enforce the tutor/user and tutor/programme foreign keys;
4. add the year and average check constraints;
5. convert approval values, then drop `EntraObjectId`, `EntraTenantId`,
   `DisplayName`, `Email`, and `IsApproved` from `Tutors`; and
6. change `Biography` to nullable `nvarchar(500)` only after the length check
   returns no rows.

Review the generated `Down` method as a data-loss operation. Do not apply stage
2 automatically.
