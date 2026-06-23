using System;
using System.Threading;
using System.Threading.Tasks;
using KP2FAChecker.Settings;
using KPPasskeyChecker.Shared.Caching;
using KPPasskeyChecker.Shared.DomainMatching;
using KPPasskeyChecker.Shared.Http;

namespace KP2FAChecker.Data
{
    public sealed class TfaDirectoryService : IDisposable
    {
        private static TfaDirectoryService _current;

        public static bool IsAvailable
        {
            get { return _current != null; }
        }

        public static TfaDirectoryService Current
        {
            get
            {
                if (_current == null)
                    throw new InvalidOperationException("TfaDirectoryService not initialized.");
                return _current;
            }
        }

        private readonly TfaApiClient _client;
        private readonly ILocalCache _cache;
        private readonly TfaSettingsStore _settings;
        private readonly string _cacheDirectory;
        private Timer _refreshTimer;
        private bool _disposed;

        public TfaDirectory Directory { get; private set; }
        public DateTimeOffset? LastRefreshed { get; private set; }
        public bool IsStale { get; private set; }
        public bool IsUsingCachedFallback { get; private set; }
        public string LastError { get; private set; }

        public event EventHandler DataRefreshed;

        private TfaDirectoryService(TfaSettingsStore settings, string cacheDirectory)
        {
            _settings = settings;
            _cacheDirectory = cacheDirectory;
            _cache = new FileSystemJsonCache(cacheDirectory);
            _client = new TfaApiClient(
                UserAgent.Build("KP2FAChecker", PluginVersion.Current, PluginVersion.RepoUrl));
        }

        public static void Initialize(TfaSettingsStore settings, string cacheDirectory)
        {
            if (_current != null) _current.Dispose();
            _current = new TfaDirectoryService(settings, cacheDirectory);
            _current.Start();
        }

        public static void Shutdown()
        {
            if (_current != null) _current.Dispose();
            _current = null;
        }

        private void Start()
        {
            DomainCandidateGenerator.InitializeAsync(_cacheDirectory);
            Task.Run(() => RefreshAsync(false));
            ScheduleTimer();
        }

        private void ScheduleTimer()
        {
            long intervalMs = (long)TimeSpan.FromHours(_settings.RefreshIntervalHours).TotalMilliseconds;
            _refreshTimer = new Timer(delegate { Task.Run(() => RefreshAsync(false)); },
                                      null, intervalMs, intervalMs);
        }

        public async Task RefreshAsync(bool force)
        {
            if (_disposed) return;

            TfaDataResult result = await _client
                .FetchAsync(_settings.Scope, _settings.VerifyPgpSignature, _cache, force)
                .ConfigureAwait(false);

            if (_disposed) return;

            Directory             = result.Directory;
            LastRefreshed         = result.FetchedAt;
            IsStale               = result.IsStale;
            IsUsingCachedFallback = result.IsFromCache;
            LastError             = result.ErrorMessage;

            if (DataRefreshed != null)
                DataRefreshed(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }
    }
}
