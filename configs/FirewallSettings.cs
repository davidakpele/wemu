namespace wenu.Configs
{
    public class FirewallSettings
    {
        public RateLimitSettings RateLimit { get; set; } = new();
        public RequestValidationSettings RequestValidation { get; set; } = new();
        public SecuritySettings Security { get; set; } = new();
        public MonitoringSettings Monitoring { get; set; } = new();
    }

    public class RateLimitSettings
    {
        public int MaxRequestsPerMinute { get; set; } = 60;
        public int BurstLimit { get; set; } = 10;
        public int BurstWindowSeconds { get; set; } = 5;
        public int BanDurationMinutes { get; set; } = 60;
        public Dictionary<string, int> SensitivePathLimits { get; set; } = new();
    }

    public class RequestValidationSettings
    {
        public long MaxRequestSizeBytes { get; set; } = 10_485_760;
        public int MaxHeaderCount { get; set; } = 50;
        public int MaxHeaderSizeBytes { get; set; } = 8192;
        public List<string> AllowedMethods { get; set; } = new();
        public List<string> BlockedContentTypes { get; set; } = new();
    }

    public class SecuritySettings
    {
        public bool EnableSqlInjectionProtection { get; set; } = true;
        public bool EnableXssProtection { get; set; } = true;
        public bool EnablePathTraversalProtection { get; set; } = true;
        public bool EnableBotProtection { get; set; } = true;
        public bool EnableSecurityHeaders { get; set; } = true;
        public List<string> IpWhitelist { get; set; } = new();
        public List<string> IpBlacklist { get; set; } = new();
    }

    public class MonitoringSettings
    {
        public bool EnableMetrics { get; set; } = true;
        public bool LogBlockedRequests { get; set; } = true;
        public bool LogRateLimitViolations { get; set; } = true;
    }
}
