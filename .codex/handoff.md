# Session Handoff

> Generated: 2026-08-27 | Branch: master

## Completed

- [x] Authored and quality-audited the new profiles prerequisite lesson at C:/Users/jocvi/Documents/Collection-of-Folders/Obsidians/Obsidian Vault/notes/Programming/Csharp/Ultimate-ASPNET-Core-Web-API/RECREATE/74-Account-Profiles-and-Default-Assignment.md.
- [x] Positioned lesson 74 immediately before 75-Basic-Authentication-03-JWT in the refined runbook and recorded the dependency decision in the RECREATE notes.
- [x] Confirmed the current solution baseline builds: dotnet build PatientBooking.App.slnx --no-restore completed with 0 warnings and 0 errors.

## Pending

- [ ] Tomorrow: carry out lesson 74 in the project, in this order:
  1. Change Patient.MedicalRecordNumber to nullable and configure a filtered unique index.
  2. Generate and apply the EF Core migration named AllowUnassignedPatientMedicalRecordNumbers.
  3. Complete UsersService.RegisterAsync by adding the default Patient, calling SaveChangesAsync, and calling CommitAsync inside the existing transaction.
  4. Register two development users at http://localhost:5045/api/auth/register; confirm one Patient row per user and a NULL medical-record number.
  5. Keep RequireConfirmedEmail enabled. Complete email confirmation before testing the JWT login flow in lesson 75.
- [ ] After lesson 74 is implemented and verified, continue with 75-Basic-Authentication-03-JWT.

## Learned

- The existing migration 20260827083107_MakePatientProfileOneToOne already makes Patients.UserId unique; the blocking defect is that RegisterAsync currently creates neither a Patient row nor a committed transaction.
- The current unique empty-string MedicalRecordNumber prevents more than one unassigned Patient. Use nullable values with a SQL Server filtered unique index.
- Program.cs sets RequireConfirmedEmail = true; profile provisioning fixes the JWT-role prerequisite but does not independently permit password login until email confirmation is completed.

## Context

- The project has pre-existing uncommitted source changes in IUsersService.cs, UsersService.cs, PatientConfiguration.cs, PatientBookingDbContextModelSnapshot.cs, AuthController.cs, and the MakePatientProfileOneToOne migration files. Do not reset or revert them.
- No project source file was modified while authoring the lesson.
- Last commit: Chore: Updates skills

