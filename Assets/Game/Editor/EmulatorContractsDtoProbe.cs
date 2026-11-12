#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LogistiCore.Backend;

namespace LogistiCore.EditorTools
{
    public readonly struct EmulatorAuthDiagnostics
    {
        public EmulatorAuthDiagnostics(
            string clientState,
            string projectId,
            string uid,
            bool signedIn,
            string functionsTarget,
            bool readAllowed)
        {
            ClientState = clientState ?? "";
            ProjectId = projectId ?? "";
            Uid = uid ?? "";
            SignedIn = signedIn;
            FunctionsTarget = functionsTarget ?? "";
            ReadAllowed = readAllowed;
        }

        public string ClientState { get; }
        public string ProjectId { get; }
        public string Uid { get; }
        public bool SignedIn { get; }
        public string FunctionsTarget { get; }
        public bool ReadAllowed { get; }
        public string AuthDisplay =>
            SignedIn && !string.IsNullOrEmpty(Uid) ? Uid : EmulatorContractsDtoProbe.NoActiveUser;
    }

    /// <summary>
    /// Editor-only getMyContracts DTO probe. Does not sign in or dispatch commands.
    /// </summary>
    public static class EmulatorContractsDtoProbe
    {
        public const string CompletedContractId = "9298bef2f37fca8a0ee1de263803936763390db67afb18b63835ecb1e0e69c54";
        public const string ButtonLabel = "Verify Contracts DTO (Read Only)";
        public const string NoActiveUser = "No active Auth user";
        public const string DtoVerified = "DTO VERIFIED";
        public const string DtoFieldMissing = "DTO FIELD MISSING";
        public const string AuthHold = "AUTH HOLD";
        public const string FunctionsReadError = "FUNCTIONS READ ERROR";
        public const string ParserHold = "PARSER HOLD";
        public const string DuplicateBlocked = "FUNCTIONS READ ERROR";

        public static EmulatorAuthDiagnostics Snapshot(
            bool playing,
            ClientEnvironmentSettings settings,
            ClientBackendStatus status,
            string currentUid)
        {
            var uid = currentUid ?? "";
            var signed = playing && !string.IsNullOrEmpty(uid);
            var project = settings != null ? settings.ProjectId : "";
            var functions = "";
            if (settings != null)
            {
                var origin = settings.FunctionsEmulatorOrigin ?? "";
                var region = settings.FunctionsRegion ?? "";
                if (origin.Length > 0 && region.Length > 0)
                    functions = origin + " / " + region;
                else
                    functions = origin + region;
            }

            var state = playing
                ? status.Phase.ToString()
                : "Editor (not Play Mode)";
            var read = playing && ClientLocalEmulatorSecurity.AllowsLiveReads(settings, status);
            return new EmulatorAuthDiagnostics(state, project, uid, signed, functions, read);
        }

        public static bool TryCanVerify(
            bool playing,
            ClientEnvironmentSettings settings,
            ClientBackendStatus status,
            string currentUid,
            ClientReadSession session,
            bool busy,
            out string hold)
        {
            hold = AuthHold;
            if (!playing)
                return false;
            if (string.IsNullOrEmpty(currentUid)
                || !string.Equals(currentUid, EmulatorAuthTestGate.ExpectedUid, StringComparison.Ordinal))
                return false;
            if (!EmulatorAuthTestGate.TryAllow(playing, settings, status, out _))
                return false;
            if (session == null || !session.HasInvoker)
            {
                hold = FunctionsReadError;
                return false;
            }

            if (busy)
            {
                hold = DuplicateBlocked;
                return false;
            }

            hold = null;
            return true;
        }

        public static bool TryBeginVerify(
            bool clicked,
            bool playing,
            ClientEnvironmentSettings settings,
            ClientBackendStatus status,
            string currentUid,
            ClientReadSession session,
            bool busy,
            int generation,
            Action<string, string, int> completed,
            out string hold)
        {
            hold = AuthHold;
            if (!clicked)
            {
                hold = EmulatorAuthTestGate.ExplicitActionRequired;
                return false;
            }

            if (!TryCanVerify(playing, settings, status, currentUid, session, busy, out hold))
                return false;

            if (completed == null)
            {
                hold = FunctionsReadError;
                return false;
            }

            var gen = generation;
            session.ReadAsync(
                ClientCallableCatalog.GetMyContracts,
                status,
                true,
                ClientValue.ReadRequest(),
                outcome =>
                {
                    string evidence;
                    var verdict = Evaluate(outcome, out evidence);
                    completed(verdict, evidence, gen);
                });
            hold = null;
            return true;
        }

        public static string Evaluate(ClientReadOutcome outcome, out string evidence)
        {
            evidence = "";
            if (!outcome.Ok)
            {
                evidence = "stage=" + outcome.FailureStage
                    + "; fault=" + outcome.Fault
                    + "; exception=" + outcome.ExceptionKind
                    + "; functions=" + outcome.FunctionsCodeName;
                return FunctionsReadError;
            }
            if (outcome.Envelope.ProtocolVersion != ClientProtocol.Version)
                return ParserHold;
            if (!ClientReadParser.TryContracts(outcome.Envelope.Result, out var page))
                return ParserHold;

            ClientContractRow found = default;
            var present = false;
            if (page.Items != null)
            {
                for (var i = 0; i < page.Items.Length; i++)
                {
                    if (page.Items[i].ContractId == CompletedContractId)
                    {
                        found = page.Items[i];
                        present = true;
                        break;
                    }
                }
            }

            if (!present)
                return DtoFieldMissing;
            if (!string.Equals(found.Status, "completed", StringComparison.Ordinal))
                return ParserHold;
            if (!found.CandidateAssetsPresent || found.CandidateAssets == null)
                return DtoFieldMissing;
            if (!HasKind(found.CandidateAssets, "driver") || !HasKind(found.CandidateAssets, "truck"))
                return DtoFieldMissing;
            evidence = Evidence(found.CandidateAssets);
            return DtoVerified;
        }

        static bool HasKind(ClientDeliveryAsset[] assets, string kind)
        {
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i].Kind == kind && !string.IsNullOrEmpty(assets[i].AssetId))
                    return true;
            }

            return false;
        }

        static string Evidence(ClientDeliveryAsset[] assets)
        {
            var parts = new List<string>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
                parts.Add(assets[i].Kind + "=" + assets[i].AssetId);
            return string.Join(", ", parts.ToArray());
        }
    }

    // Passive inspection only: no SDK getters, factories, token methods or static initializers.
    public static class EmulatorPassiveAuthDiagnostics
    {
        public enum Observation { NotObserved, NotPlaying, StaticInitializationGuarded, MetadataUnavailable, Missing, Present, Match, Mismatch, Ambiguous }

        public readonly struct Snapshot
        {
            public Snapshot(Observation bridge, Observation auth, Observation functions, Observation appMatch, Observation localTarget)
            { Bridge = bridge; Auth = auth; Functions = functions; AppMatch = appMatch; LocalTarget = localTarget; }
            public Observation Bridge { get; }
            public Observation Auth { get; }
            public Observation Functions { get; }
            public Observation AppMatch { get; }
            public Observation LocalTarget { get; }
            public override string ToString() => "bridge=" + Bridge + "; cachedAuth=" + Auth
                + "; cachedFunctions=" + Functions + "; appMatch=" + AppMatch + "; localTarget=" + LocalTarget;
        }

        public static Snapshot Capture(bool playing)
        {
            if (!playing)
                return new Snapshot(Observation.NotPlaying, Observation.NotPlaying, Observation.NotPlaying, Observation.NotPlaying, Observation.NotPlaying);
            try
            {
                // GetValue on an uninitialized static type can execute its .cctor.
                // Never read FirebaseInterops static fields: readiness stays unobserved.
                var runtimeType = typeof(ClientJobsRuntime);
                if (runtimeType.TypeInitializer != null)
                    return new Snapshot(Observation.StaticInitializationGuarded, Observation.StaticInitializationGuarded,
                        Observation.NotObserved, Observation.NotObserved, Observation.NotObserved);
                var authField = runtimeType.GetField("_auth", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (authField == null)
                    return Unknown();
                var auth = authField.GetValue(null);
                if (auth == null)
                    return new Snapshot(Observation.StaticInitializationGuarded, Observation.Missing,
                        Observation.NotObserved, Observation.NotObserved, Observation.NotObserved);
                var appField = InstanceField(auth.GetType(), "appProxy");
                if (appField == null)
                    return Unknown();
                var app = appField.GetValue(auth);
                if (app == null)
                    return Unknown();
                var eventField = InstanceField(app.GetType(), "AppDisposed");
                if (eventField == null)
                    return Unknown();
                var handlers = eventField.GetValue(app) as Delegate;
                object functions = null;
                if (handlers != null)
                {
                    foreach (var handler in handlers.GetInvocationList())
                    {
                        var target = handler.Target;
                        if (target == null || target.GetType().FullName != "Firebase.Functions.FirebaseFunctions")
                            continue;
                        var region = InstanceField(target.GetType(), "_region");
                        if (region == null || !string.Equals(region.GetValue(target) as string, ClientProtocol.FunctionsRegion, StringComparison.Ordinal))
                            continue;
                        if (functions != null && !ReferenceEquals(functions, target))
                            return new Snapshot(Observation.StaticInitializationGuarded, Observation.Present,
                                Observation.Ambiguous, Observation.NotObserved, Observation.NotObserved);
                        functions = target;
                    }
                }
                if (functions == null)
                    return new Snapshot(Observation.StaticInitializationGuarded, Observation.Present,
                        Observation.NotObserved, Observation.NotObserved, Observation.NotObserved);
                var functionsApp = InstanceField(functions.GetType(), "_firebaseApp");
                var origin = InstanceField(functions.GetType(), "_emulator_origin");
                return new Snapshot(Observation.StaticInitializationGuarded, Observation.Present, Observation.Present,
                    functionsApp == null ? Observation.MetadataUnavailable
                        : CompareApps(app, functionsApp.GetValue(functions)),
                    origin == null ? Observation.MetadataUnavailable
                        : string.Equals(origin.GetValue(functions) as string, ClientLocalEmulatorSecurity.FunctionsOrigin, StringComparison.Ordinal)
                            ? Observation.Match : Observation.Mismatch);
            }
            catch (Exception)
            {
                // Do not log exception messages, object dumps or SDK state.
                return Unknown();
            }
        }

        public static Observation CompareApps(object authApp, object functionsApp)
        {
            if (authApp == null || functionsApp == null)
                return Observation.NotObserved;
            return ReferenceEquals(authApp, functionsApp) ? Observation.Match : Observation.Mismatch;
        }

        static System.Reflection.FieldInfo InstanceField(Type type, string name) =>
            type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        static Snapshot Unknown() => new Snapshot(Observation.StaticInitializationGuarded, Observation.MetadataUnavailable,
            Observation.NotObserved, Observation.NotObserved, Observation.NotObserved);
    }

}
#endif
