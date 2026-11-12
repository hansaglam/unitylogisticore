#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using Firebase.Auth;
using LogistiCore.Backend;
using UnityEditor;
using UnityEngine;

namespace LogistiCore.EditorTools
{
    /// <summary>
    /// Play Mode only. Signs in the existing emulator test user on the shared FirebaseAuth.
    /// Does not call HandleAuth; Auth StateChanged drives Jobs.
    /// </summary>
    public sealed class EmulatorAuthTestWindow : EditorWindow
    {
        [NonSerialized] string _password = "";
        [NonSerialized] string _status = "";
        [NonSerialized] bool _busy;
        [NonSerialized] Task<AuthResult> _pending;
        [NonSerialized] bool _dtoBusy;
        [NonSerialized] int _dtoGeneration;
        [NonSerialized] string _dtoVerdict = "";
        [NonSerialized] string _dtoEvidence = "";
        [NonSerialized] string _seenUid = "";
        [NonSerialized] string _passiveAuthSnapshot = "";

        [MenuItem(EmulatorAuthTestGate.MenuPath)]
        public static void Open()
        {
            var window = GetWindow<EmulatorAuthTestWindow>("Emulator Auth Test");
            window.minSize = new Vector2(380f, 340f);
            window.hideFlags = HideFlags.DontSave;
        }

        void OnEnable()
        {
            EditorApplication.update -= RepaintIfPlaying;
            EditorApplication.update += RepaintIfPlaying;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.update -= PollSignIn;
            EditorApplication.update -= RepaintIfPlaying;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            WipePassword();
        }

        void OnDestroy()
        {
            EditorApplication.update -= PollSignIn;
            EditorApplication.update -= RepaintIfPlaying;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            WipePassword();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Local emulator test sign-in", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play Mode only. Uses the existing FirebaseAuth instance. " +
                "Does not create users. Jobs refresh via Auth StateChanged.",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Email", ClientEmulatorAuthCredentials.Email);
            EditorGUI.EndDisabledGroup();

            using (new EditorGUI.DisabledScope(_busy))
            {
                _password = EditorGUILayout.PasswordField("Password", _password ?? "") ?? "";

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Sign in"))
                    RequestSignIn();
                if (GUILayout.Button("Sign out"))
                    RequestSignOut();
                EditorGUILayout.EndHorizontal();
            }

            var shown = DisplayStatus();
            if (!string.IsNullOrEmpty(shown))
                EditorGUILayout.HelpBox(shown, MessageType.None);

            DrawDiagnostics();
            DrawDtoProbe();
            if (GUILayout.Button("Inspect SDK Auth / Functions (No Network)"))
                _passiveAuthSnapshot = EmulatorPassiveAuthDiagnostics.Capture(EditorApplication.isPlaying).ToString()
                    + "\n" + ClientReadLifecycleTrace.Snapshot()
                    + "\nLifecycle user presence is last observed, not token readiness. CallAsyncEntered is a local call boundary, not proof of an HTTP send. "
                    + "Generation resets on domain reload; only the last 24 events are retained.";
            if (!string.IsNullOrEmpty(_passiveAuthSnapshot))
                EditorGUILayout.HelpBox(_passiveAuthSnapshot + "\nBridge readiness is not read: static field access could initialize it. "
                    + "App match compares existing object references only; it does not verify token validity or delivery. "
                    + "Snapshot applies only to the current session.", MessageType.None);
        }

        void DrawDiagnostics()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play Mode Auth diagnostics", EditorStyles.boldLabel);
            var snap = CurrentDiagnostics();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Firebase Client State", snap.ClientState);
            EditorGUILayout.TextField("Firebase Project ID", string.IsNullOrEmpty(snap.ProjectId) ? "(none)" : snap.ProjectId);
            EditorGUILayout.TextField("Auth CurrentUser UID", snap.AuthDisplay);
            EditorGUILayout.TextField("Auth signed-in", snap.SignedIn ? "yes" : "no");
            EditorGUILayout.TextField("Functions target", string.IsNullOrEmpty(snap.FunctionsTarget) ? "(none)" : snap.FunctionsTarget);
            EditorGUILayout.TextField("Read-only permission", snap.ReadAllowed ? "allowed" : "denied");
            EditorGUI.EndDisabledGroup();
        }

        void DrawDtoProbe()
        {
            EditorGUILayout.Space();
            var uid = ReadCurrentUid();
            var settings = ClientJobsRuntime.Settings ?? ClientEnvironmentSettings.Load();
            var status = FirebaseClient.Current;
            var session = ClientJobsRuntime.Session;
            var can = EmulatorContractsDtoProbe.TryCanVerify(
                EditorApplication.isPlaying,
                settings,
                status,
                uid,
                session,
                _dtoBusy,
                out _);
            using (new EditorGUI.DisabledScope(!can))
            {
                if (GUILayout.Button(EmulatorContractsDtoProbe.ButtonLabel))
                    RequestDtoVerify();
            }

            if (!string.IsNullOrEmpty(_dtoVerdict))
            {
                var detail = _dtoVerdict;
                if (!string.IsNullOrEmpty(_dtoEvidence))
                    detail += "\n" + _dtoEvidence;
                EditorGUILayout.HelpBox(detail, MessageType.None);
            }
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            _passiveAuthSnapshot = "";
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
                ClearDtoResult();
        }

        void RepaintIfPlaying()
        {
            if (!EditorApplication.isPlaying)
                return;
            var uid = ReadCurrentUid();
            if (uid != _seenUid)
            {
                _seenUid = uid;
                _dtoGeneration++;
                ClearDtoResult();
            }

            Repaint();
        }

        void ClearDtoResult()
        {
            _dtoBusy = false;
            _dtoVerdict = "";
            _dtoEvidence = "";
        }

        static string ReadCurrentUid()
        {
            if (!EditorApplication.isPlaying)
                return "";
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                var user = auth != null ? auth.CurrentUser : null;
                return user != null ? user.UserId ?? "" : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        EmulatorAuthDiagnostics CurrentDiagnostics()
        {
            var settings = ClientJobsRuntime.Settings ?? ClientEnvironmentSettings.Load();
            return EmulatorContractsDtoProbe.Snapshot(
                EditorApplication.isPlaying,
                settings,
                FirebaseClient.Current,
                ReadCurrentUid());
        }

        void RequestDtoVerify()
        {
            var uid = ReadCurrentUid();
            var settings = ClientJobsRuntime.Settings ?? ClientEnvironmentSettings.Load();
            var status = FirebaseClient.Current;
            var session = ClientJobsRuntime.Session;
            var generation = ++_dtoGeneration;
            _dtoBusy = true;
            _dtoVerdict = "";
            _dtoEvidence = "";
            if (!EmulatorContractsDtoProbe.TryBeginVerify(
                true,
                EditorApplication.isPlaying,
                settings,
                status,
                uid,
                session,
                false,
                generation,
                (verdict, evidence, gen) =>
                {
                    if (gen != _dtoGeneration)
                        return;
                    _dtoBusy = false;
                    _dtoVerdict = verdict ?? "";
                    _dtoEvidence = evidence ?? "";
                    Repaint();
                },
                out var hold))
            {
                _dtoBusy = false;
                _dtoVerdict = string.IsNullOrEmpty(hold) ? EmulatorContractsDtoProbe.AuthHold : hold;
            }
        }

        string DisplayStatus()
        {
            if (_busy || !EditorApplication.isPlaying)
                return _status;

            FirebaseUser user = null;
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                user = auth != null ? auth.CurrentUser : null;
            }
            catch (Exception)
            {
                user = null;
            }

            if (user != null && string.Equals(user.UserId, EmulatorAuthTestGate.ExpectedUid, StringComparison.Ordinal))
                return "Signed in. Jobs uses Auth StateChanged.";
            if (user == null && !string.IsNullOrEmpty(_status) && _status.IndexOf("Signed in", StringComparison.Ordinal) >= 0)
                return "Not signed in this Play session.";
            return _status;
        }

        void RequestSignIn()
        {
            string fault;
            if (!EmulatorAuthTestGate.TryAllow(
                EditorApplication.isPlaying,
                ClientEnvironmentSettings.Load(),
                FirebaseClient.Current,
                out fault))
            {
                WipePassword();
                _status = fault;
                return;
            }

            if (!ClientEmulatorAuthPolicy.IsSyntheticTestEmail(ClientEmulatorAuthCredentials.Email))
            {
                WipePassword();
                _status = EmulatorAuthTestGate.EmailNotAllowed;
                return;
            }

            if (string.IsNullOrEmpty(_password))
            {
                _status = EmulatorAuthTestGate.PasswordRequired;
                return;
            }

            FirebaseAuth auth;
            try
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception)
            {
                auth = null;
            }

            if (auth == null)
            {
                WipePassword();
                _status = EmulatorAuthTestGate.NotReady;
                return;
            }

            var password = _password;
            WipePassword();
            GUI.FocusControl(null);
            _busy = true;
            _status = "Signing in…";
            try
            {
                _pending = auth.SignInWithEmailAndPasswordAsync(
                    ClientEmulatorAuthCredentials.Email,
                    password);
            }
            catch (Exception)
            {
                _busy = false;
                _pending = null;
                _status = EmulatorAuthTestGate.SignInFailed;
                WipeString(ref password);
                return;
            }

            WipeString(ref password);
            EditorApplication.update -= PollSignIn;
            EditorApplication.update += PollSignIn;
        }

        void PollSignIn()
        {
            if (_pending == null || !_pending.IsCompleted)
            {
                if (!EditorApplication.isPlaying)
                    CancelPending();
                return;
            }

            EditorApplication.update -= PollSignIn;
            var task = _pending;
            _pending = null;
            _busy = false;

            if (task.IsFaulted || task.IsCanceled || task.Result == null || task.Result.User == null)
            {
                _status = EmulatorAuthTestGate.SignInFailed;
                Repaint();
                return;
            }

            var uid = task.Result.User.UserId ?? "";
            if (!string.Equals(uid, EmulatorAuthTestGate.ExpectedUid, StringComparison.Ordinal))
            {
                LiveSignOut();
                _status = EmulatorAuthTestGate.UnexpectedAccount;
                Repaint();
                return;
            }

            _status = "Signed in. Jobs uses Auth StateChanged.";
            Repaint();
        }

        void CancelPending()
        {
            EditorApplication.update -= PollSignIn;
            _pending = null;
            _busy = false;
            WipePassword();
            _status = EmulatorAuthTestGate.PlayModeRequired;
        }

        void RequestSignOut()
        {
            WipePassword();
            LiveSignOut();
            _status = "Signed out.";
            Repaint();
        }

        static void LiveSignOut()
        {
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                if (auth != null)
                    auth.SignOut();
            }
            catch (Exception)
            {
            }
        }

        void WipePassword()
        {
            WipeString(ref _password);
            _password = "";
        }

        static void WipeString(ref string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                value = "";
                return;
            }

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                chars[i] = '\0';
            value = new string('\0', chars.Length);
            value = "";
        }
    }
}
#endif
