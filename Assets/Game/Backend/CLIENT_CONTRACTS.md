# Client backend contracts pending

UNITY-01A prepares a client-only Firebase gate. It does not define the backend API.

The committed environment asset is Unconfigured and empty. `LocalClientEnvironment` is gitignored and, when present, overrides that asset. Missing development or emulator configuration stays refused. Production is always refused. The client does not sign in, create users, read Firestore, or call Cloud Functions.

These contracts are still required from the backend owner before any client integration:

1. Authentication providers the client may start. No provider is assumed. Development accounts are not assumed to match production accounts.
2. Callable function names, regions, request payloads, and response payloads.
3. Which reads are direct client Firestore reads, and the collection paths for those reads. Everything else stays behind a callable.
4. The error envelope the client may display.
5. The server-time source.
6. For an approved non-production project only: project id, app id, and API key, supplied outside git.
7. For the local emulator only: host, Firestore port, Auth port, Functions origin, and Functions region. The client does not fill these in.

Until those contracts exist, client repositories, command payloads, and login screens stay unimplemented.
