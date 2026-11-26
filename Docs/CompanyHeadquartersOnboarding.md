# CITY-START-03 — Explicit HQ company creation onboarding (emulator-only)

## Completed
- Reused the existing country/city selector in \`CompanyHeadquartersPreview\` (248 launch city points) and added an editable company name, a visible review summary, a preflight button and a **separate explicit confirmation** action.
- The proposal must use a valid launch city and plausible company name. Changing the name/city resets eligibility; during a server read/write the input widgets are disabled. On confirmation, the UI locks the chosen center and the existing **Preview on map** action remains available.
- Before company creation the form reads trusted \`getMyCompanyState\`. If an authenticated company already exists, it **never permits** creation and reports that existing progress is preserved.
- If the read says no company, the confirmation button is enabled **only** when the Firebase SDK is ready, the user is signed in, the backend is the exact \`demo-logisticore-local\` loopback Firebase emulator, and the owner opted in to the company mutation window (\`LOGISTICORE_EMU_LIVE=company\` or the existing test authorization). The production/unknown environment remains inaccessible. No auto-signup/password form or account creation was added.
- \`ClientHeadquartersCreationGateway\` is a narrow one-command client transport to \`createCompanyWithHeadquarters\`. It does **not** lift the existing \`ClientCommandAuthorization.AllowsLiveFunctions == false\` general gameplay command hold.
- The command uses the existing protocolVersion-1, generated idempotency-key envelope. After uncertain network results the UI does **not** report success or automatically retry; the user must explicitly recheck the server company state. The same chosen company/location cannot be silently changed mid-flight.
- A confirmed success result is checked against the selected company name, country code, city key and pinned HQ catalog version before the UI displays a saved message. It does not grant depots, truck access, contracts or cash.
- Interface copy for this new flow is provided in Turkish, English and German; source city names are not yet fully localized. The existing Company portrait screen scrolls to accommodate the additional fields.
- Added deferred Unity EditMode tests and updated the existing Company screen structure contract.

## Security and compatibility
- \`getMyCompanyState\` and \`createCompany\` remain backward compatible; CITY-START-02 provides an optional \`getMyCompanyHeadquarters\` for reading the new HQ sidecar record.
- Existing players' companies are never overwritten or auto-migrated. Existing players with no HQ record retain their current map experience.
- Client is not the source of truth for creation success or player identity; the backend validates account ownership, new-company eligibility, city allowlist, transactional writes and idempotency.
- Backend remains strictly local-emulator-only under \`assertLocalEnvironment\`. No production Firebase project or deployed database is accessed by this development.
- This feature is **not a public launch flow**: the emulator must have an existing signed-in test account with \`ensurePlayerIdentity\` completed. The default app/environment resource remains unconfigured, so by default this form is a review/preview only. No live production connection, account bootstrap or shipping contract path was opened.

## Next developments
1. Implement \`CITY-START-04\`: read and verify HQ from the authenticated backend after login, focus map on verified HQ, display an explicit "unassigned legacy HQ" state for old companies without writing to them.
2. Implement vetted starting depot/fleet provisioning for launch countries; a country/city being in the geographic catalog alone does **not** prove a drivable road route, an available depot or economic eligibility.
3. Implement \`ROUTE-01\` via a legally usable OSM-based routing provider and server-owned route quotes/delivery access. Add stronger Germany/Benelux/Poland hubs to ensure meaningful European starting regions.
4. When the project has a reviewed production backend and migration strategy, design staged live onboarding and recovery for existing app players, with analytics and localization QA.

## Final validation checkpoint
Per development workflow agreement, Unity compile, EditMode/PlayMode tests, Firebase emulator tests, iOS/Android UI inspection and migration/authorization regression are **not yet executed**. This branch is a stacked draft and must not be merged directly into main. Do not treat the UI as verified or deployable.
