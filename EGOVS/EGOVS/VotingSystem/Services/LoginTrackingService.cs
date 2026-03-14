using Microsoft.Data.SqlClient;
using System.Text;
using VotingSystem.Data;
using VotingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace VotingSystem.Services
{
    public interface ILoginTrackingService
    {
        Task LogLoginAttemptAsync(string nationalId, string role, string status, string description, HttpContext httpContext, string additionalData = null);
        Task LogUserActivityAsync(string nationalId, string role, string action, string description, HttpContext httpContext, string additionalData = null);
        Task CheckAndCreateSecurityAlertAsync(string nationalId, string role, string status, string description, HttpContext httpContext);
    }

    public class LoginTrackingService : ILoginTrackingService
    {
        private readonly string _connectionString;
        private readonly ILogger<LoginTrackingService> _logger;
        private readonly AppDbContext _context;

        public LoginTrackingService(IConfiguration configuration, ILogger<LoginTrackingService> logger, AppDbContext context)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _context = context;
        }

        public async Task LogLoginAttemptAsync(string nationalId, string role, string status, string description, HttpContext httpContext, string additionalData = null)
        {
            // Map role to valid database values
            string validRole = MapRoleToValidValue(role);
            string validStatus = MapStatusToValidValue(status);
            
            await LogActivityAsync(nationalId, validRole, "Login Attempt", description, httpContext, validStatus, additionalData);
            
            // Check for security alerts
            await CheckAndCreateSecurityAlertAsync(nationalId, role, status, description, httpContext);
        }

        public async Task LogUserActivityAsync(string nationalId, string role, string action, string description, HttpContext httpContext, string additionalData = null)
        {
            // Map role to valid database values
            string validRole = MapRoleToValidValue(role);
            
            await LogActivityAsync(nationalId, validRole, action, description, httpContext, "Success", additionalData);
        }

        // NEW: Security Alert Generation
        public async Task CheckAndCreateSecurityAlertAsync(string nationalId, string role, string status, string description, HttpContext httpContext)
        {
            try
            {
                // Check for multiple failed login attempts
                if (status == "Failed")
                {
                    var timeThreshold = DateTime.Now.AddMinutes(-15);
                    
                    var failedAttempts = await _context.SystemActivityLogs
                        .Where(log => log.NationalId == nationalId 
                                   && log.Action == "Login Attempt" 
                                   && log.Status == "Failed"
                                   && log.Timestamp >= timeThreshold)
                        .CountAsync();

                    if (failedAttempts >= 3) // Alert after 3 failed attempts
                    {
                        var alert = new SecurityAlert
                        {
                            AlertType = "Multiple Failed Login Attempts",
                            Description = $"User {nationalId} ({role}) has {failedAttempts} failed login attempts in the last 15 minutes. Possible brute force attack.",
                            Severity = failedAttempts >= 5 ? "Critical" : "High",
                            AlertDate = DateTime.Now,
                            IsResolved = false,
                            NationalId = nationalId,
                            Role = role,
                            AdditionalData = $"IP: {GetClientIpAddress(httpContext)}, Attempts: {failedAttempts}"
                        };

                        _context.SecurityAlerts.Add(alert);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogWarning("SECURITY ALERT: Multiple failed login attempts for {NationalId}", nationalId);
                    }
                }

                // Check for login from unusual location (basic implementation)
                // You can enhance this with IP geolocation
                var recentLogins = await _context.SystemActivityLogs
                    .Where(log => log.NationalId == nationalId 
                               && log.Action == "Login Attempt" 
                               && log.Status == "Success"
                               && log.Timestamp >= DateTime.Now.AddHours(-24))
                    .OrderByDescending(log => log.Timestamp)
                    .Take(5)
                    .ToListAsync();

                if (recentLogins.Count >= 2)
                {
                    var currentIp = GetClientIpAddress(httpContext);
                    var previousIp = recentLogins.Skip(1).FirstOrDefault()?.IpAddress;
                    
                    if (!string.IsNullOrEmpty(previousIp) && currentIp != previousIp && status == "Success")
                    {
                        var alert = new SecurityAlert
                        {
                            AlertType = "Login from New Location",
                            Description = $"User {nationalId} ({role}) logged in from a new IP address. Previous: {previousIp}, Current: {currentIp}",
                            Severity = "Medium",
                            AlertDate = DateTime.Now,
                            IsResolved = false,
                            NationalId = nationalId,
                            Role = role,
                            AdditionalData = $"Previous IP: {previousIp}, Current IP: {currentIp}"
                        };

                        _context.SecurityAlerts.Add(alert);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogWarning("SECURITY ALERT: Login from new location for {NationalId}", nationalId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating security alert for {NationalId}", nationalId);
            }
        }

        private async Task LogActivityAsync(string nationalId, string role, string action, string description, HttpContext httpContext, string status, string additionalData)
        {
            try
            {
                var log = new SystemActivityLog
                {
                    NationalId = nationalId ?? "SYSTEM",
                    Role = role,
                    Action = action,
                    Description = TruncateDescription(description, 500),
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                    Timestamp = DateTime.Now,
                    Status = status,
                    AdditionalData = additionalData
                };

                _context.SystemActivityLogs.Add(log);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Logged system activity: {NationalId}, {Role}, {Action}, {Status}", nationalId, role, action, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log system activity for NationalId: {NationalId}, Role: {Role}, Action: {Action}", nationalId, role, action);
                // Fallback to SQL if Entity Framework fails
                await LogActivitySqlAsync(nationalId, role, action, description, httpContext, status, additionalData);
            }
        }

        private async Task LogActivitySqlAsync(string nationalId, string role, string action, string description, HttpContext httpContext, string status, string additionalData)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        INSERT INTO SystemActivityLogs 
                        (NationalId, Role, Action, Description, IpAddress, UserAgent, Status, AdditionalData) 
                        VALUES (@NationalId, @Role, @Action, @Description, @IpAddress, @UserAgent, @Status, @AdditionalData)", 
                        connection);

                    command.Parameters.AddWithValue("@NationalId", nationalId ?? "SYSTEM");
                    command.Parameters.AddWithValue("@Role", role);
                    command.Parameters.AddWithValue("@Action", action);
                    command.Parameters.AddWithValue("@Description", TruncateDescription(description, 500));
                    command.Parameters.AddWithValue("@IpAddress", GetClientIpAddress(httpContext) ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@UserAgent", httpContext?.Request?.Headers["User-Agent"].ToString() ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@AdditionalData", additionalData ?? (object)DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log system activity via SQL for NationalId: {NationalId}", nationalId);
            }
        }

        // ========== HELPER METHODS ==========
        private string MapRoleToValidValue(string role)
        {
            if (string.IsNullOrEmpty(role) || role == "Unknown")
                return "System";
                
            var validRoles = new[] { "Voter", "Admin", "Supervisor", "Manager", "Candidate", "System" };
            return validRoles.Contains(role) ? role : "System";
        }

        private string MapStatusToValidValue(string status)
        {
            if (string.IsNullOrEmpty(status))
                return "Attempt";
                
            var validStatuses = new[] { "Success", "Failed", "Attempt", "Warning" };
            return validStatuses.Contains(status) ? status : "Attempt";
        }

        private string TruncateDescription(string description, int maxLength)
        {
            if (string.IsNullOrEmpty(description) || description.Length <= maxLength)
                return description ?? string.Empty;
                
            return description.Substring(0, maxLength - 3) + "...";
        }

        private string GetClientIpAddress(HttpContext httpContext)
        {
            try
            {
                // Get the originating IP address (considering proxies)
                var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();

                // Check for forwarded header (behind proxy/load balancer)
                if (httpContext?.Request?.Headers != null)
                {
                    if (httpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
                    {
                        ip = httpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
                    }
                    else if (httpContext.Request.Headers.ContainsKey("X-Real-IP"))
                    {
                        ip = httpContext.Request.Headers["X-Real-IP"].ToString();
                    }
                }

                return ip ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        // ========== ADDITIONAL SECURITY ALERT METHODS ==========
        
        // Method to check for suspicious voting patterns
        public async Task CheckVotingPatternsAsync(string voterNationalId, HttpContext httpContext)
        {
            try
            {
                // Check if voter has voted multiple times in short period
                var recentVotes = await _context.Votes
                    .Where(v => v.VoterNationalId == voterNationalId 
                             && v.VoteDate >= DateTime.Now.AddHours(-1))
                    .CountAsync();

                if (recentVotes > 1)
                {
                    var alert = new SecurityAlert
                    {
                        AlertType = "Suspicious Voting Pattern",
                        Description = $"Voter {voterNationalId} has cast {recentVotes} votes within 1 hour. Possible voting abuse.",
                        Severity = "High",
                        AlertDate = DateTime.Now,
                        IsResolved = false,
                        NationalId = voterNationalId,
                        Role = "Voter",
                        AdditionalData = $"IP: {GetClientIpAddress(httpContext)}, Votes in last hour: {recentVotes}"
                    };

                    _context.SecurityAlerts.Add(alert);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogWarning("SECURITY ALERT: Suspicious voting pattern for {VoterNationalId}", voterNationalId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking voting patterns for {VoterNationalId}", voterNationalId);
            }
        }

        // Method to check for system-wide security issues
        public async Task CheckSystemWideSecurityAsync()
        {
            try
            {
                var oneHourAgo = DateTime.Now.AddHours(-1);
                
                // Check for high rate of failed logins system-wide
                var totalFailedLogins = await _context.SystemActivityLogs
                    .Where(log => log.Action == "Login Attempt" 
                               && log.Status == "Failed"
                               && log.Timestamp >= oneHourAgo)
                    .CountAsync();

                if (totalFailedLogins > 50) // High threshold for system-wide attack
                {
                    var alert = new SecurityAlert
                    {
                        AlertType = "System Under Attack",
                        Description = $"High rate of failed login attempts detected: {totalFailedLogins} in the last hour. Possible coordinated attack.",
                        Severity = "Critical",
                        AlertDate = DateTime.Now,
                        IsResolved = false,
                        NationalId = "SYSTEM",
                        Role = "System",
                        AdditionalData = $"Total failed attempts: {totalFailedLogins}"
                    };

                    _context.SecurityAlerts.Add(alert);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogWarning("SECURITY ALERT: System under attack - {FailedAttempts} failed logins", totalFailedLogins);
                }

                // Check for unusual admin activity
                var adminActivities = await _context.SystemActivityLogs
                    .Where(log => log.Role == "Admin" 
                               && log.Timestamp >= oneHourAgo
                               && (log.Action.Contains("Delete") || log.Action.Contains("Create") || log.Action.Contains("Update")))
                    .CountAsync();

                if (adminActivities > 20) // High admin activity threshold
                {
                    var alert = new SecurityAlert
                    {
                        AlertType = "Unusual Admin Activity",
                        Description = $"High rate of administrative changes detected: {adminActivities} in the last hour.",
                        Severity = "High",
                        AlertDate = DateTime.Now,
                        IsResolved = false,
                        NationalId = "SYSTEM",
                        Role = "System",
                        AdditionalData = $"Admin activities: {adminActivities}"
                    };

                    _context.SecurityAlerts.Add(alert);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogWarning("SECURITY ALERT: Unusual admin activity - {Activities} changes", adminActivities);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system-wide security");
            }
        }

        // Method to get security statistics for dashboard
        public async Task<SecurityStatistics> GetSecurityStatisticsAsync()
        {
            try
            {
                var twentyFourHoursAgo = DateTime.Now.AddHours(-24);
                
                var totalAlerts = await _context.SecurityAlerts
                    .Where(a => a.AlertDate >= twentyFourHoursAgo)
                    .CountAsync();

                var criticalAlerts = await _context.SecurityAlerts
                    .Where(a => a.AlertDate >= twentyFourHoursAgo && a.Severity == "Critical" && !a.IsResolved)
                    .CountAsync();

                var failedLogins = await _context.SystemActivityLogs
                    .Where(log => log.Action == "Login Attempt" 
                               && log.Status == "Failed"
                               && log.Timestamp >= twentyFourHoursAgo)
                    .CountAsync();

                var successfulLogins = await _context.SystemActivityLogs
                    .Where(log => log.Action == "Login Attempt" 
                               && log.Status == "Success"
                               && log.Timestamp >= twentyFourHoursAgo)
                    .CountAsync();

                return new SecurityStatistics
                {
                    TotalAlertsLast24h = totalAlerts,
                    CriticalAlerts = criticalAlerts,
                    FailedLoginAttempts = failedLogins,
                    SuccessfulLogins = successfulLogins,
                    LoginSuccessRate = successfulLogins + failedLogins > 0 ? 
                        (double)successfulLogins / (successfulLogins + failedLogins) * 100 : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security statistics");
                return new SecurityStatistics(); // Return empty statistics on error
            }
        }
    }

    // ========== SUPPORTING MODELS ==========
    public class SecurityStatistics
    {
        public int TotalAlertsLast24h { get; set; }
        public int CriticalAlerts { get; set; }
        public int FailedLoginAttempts { get; set; }
        public int SuccessfulLogins { get; set; }
        public double LoginSuccessRate { get; set; }
    }
}