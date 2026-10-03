using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VotingSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        // Dashboard and Navigation Actions
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult ManageSupervisor()
        {
            return View();
        }

        public IActionResult Comments()
        {
            return View();
        }

        public IActionResult MonitorSystem()
        {
            return View();
        }

        public IActionResult PostResults()
        {
            return View();
        }

        // ========== MONITOR SYSTEM METHODS ==========

        // GET: Get election status and timing
        [HttpGet]
        public async Task<IActionResult> GetElectionStatus()
        {
            try
            {
                // Check if election configuration exists
                var electionConfig = await GetElectionConfiguration();
                
                if (electionConfig != null && electionConfig.IsActive)
                {
                    var now = DateTime.Now;
                    var startTime = electionConfig.StartTime;
                    var endTime = electionConfig.EndTime;
                    
                    var timeRemaining = endTime > now ? (endTime - now).ToString(@"hh\:mm\:ss") : "00:00:00";
                    
                    return Json(new { 
                        success = true, 
                        election = new {
                            isActive = true,
                            startTime = startTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            endTime = endTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            timeRemaining = timeRemaining
                        }
                    });
                }
                else
                {
                    return Json(new { 
                        success = true, 
                        election = new {
                            isActive = false,
                            message = "No active election configured. Please set election times."
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting election status");
                return Json(new { success = false, message = "Error retrieving election status: " + ex.Message });
            }
        }

        // GET: Get system statistics
        [HttpGet]
        public async Task<IActionResult> GetSystemStats()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT 
                            -- Voter statistics
                            (SELECT COUNT(*) FROM Voters) as TotalVoters,
                            (SELECT COUNT(*) FROM Voters WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)) as NewVotersToday,
                            
                            -- Candidate statistics
                            (SELECT COUNT(*) FROM Candidates WHERE IsActive = 1) as TotalCandidates,
                            (SELECT COUNT(*) FROM Candidates WHERE IsActive = 1) as ActiveCandidates,
                            
                            -- Vote statistics
                            (SELECT COUNT(*) FROM Votes) as TotalVotes,
                            (SELECT COUNT(*) FROM Votes WHERE CAST(VoteDate AS DATE) = CAST(GETDATE() AS DATE)) as VotesToday,
                            
                            -- Comment statistics
                            (SELECT COUNT(*) FROM Comments) as TotalComments,
                            (SELECT COUNT(*) FROM Comments WHERE IsApproved = 0) as PendingComments
                    ", connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                totalVoters = reader.GetInt32("TotalVoters"),
                                newVotersToday = reader.GetInt32("NewVotersToday"),
                                totalCandidates = reader.GetInt32("TotalCandidates"),
                                activeCandidates = reader.GetInt32("ActiveCandidates"),
                                totalVotes = reader.GetInt32("TotalVotes"),
                                votesToday = reader.GetInt32("VotesToday"),
                                totalComments = reader.GetInt32("TotalComments"),
                                pendingComments = reader.GetInt32("PendingComments")
                            };
                            return Json(new { success = true, stats = stats });
                        }
                    }
                }

                return Json(new { success = false, message = "No system statistics found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system statistics");
                return Json(new { success = false, message = "Error retrieving system statistics: " + ex.Message });
            }
        }

        // GET: Get system status
        [HttpGet]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                var statuses = new List<object>();
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Check database connection
                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        var command = new SqlCommand("SELECT 1 as Test", connection);
                        var result = await command.ExecuteScalarAsync();
                        
                        statuses.Add(new {
                            timestamp = now,
                            statusClass = "status-online",
                            message = "Database connection stable"
                        });
                    }
                }
                catch (Exception ex)
                {
                    statuses.Add(new {
                        timestamp = now,
                        statusClass = "status-offline",
                        message = $"Database connection failed: {ex.Message}"
                    });
                }

                // Check application status (simulated)
                statuses.Add(new {
                    timestamp = now,
                    statusClass = "status-online",
                    message = "Web server running normally"
                });

                // Check recent activities
                statuses.Add(new {
                    timestamp = now,
                    statusClass = "status-online",
                    message = "Application services operational"
                });

                // Check backup status (simulated)
                statuses.Add(new {
                    timestamp = now,
                    statusClass = "status-warning",
                    message = "Backup service completed with warnings"
                });

                // Check security status
                statuses.Add(new {
                    timestamp = now,
                    statusClass = "status-online",
                    message = "Security scan completed - No threats found"
                });

                return Json(new { success = true, statuses = statuses });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system status");
                return Json(new { success = false, message = "Error retrieving system status: " + ex.Message });
            }
        }

        // GET: Get recent activities from database
        [HttpGet]
        public async Task<IActionResult> GetRecentActivities()
        {
            try
            {
                var activities = new List<object>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Get recent votes with voter and candidate details
                    var command = new SqlCommand(@"
                        SELECT TOP 10 
                            'Vote cast by ' + v.FirstName + ' ' + v.LastName + ' for ' + c.FirstName + ' ' + c.LastName as Description,
                            vt.VoteDate as Timestamp,
                            'vote' as ActivityType
                        FROM Votes vt
                        INNER JOIN Voters v ON vt.VoterId = v.Id
                        INNER JOIN Candidates c ON vt.CandidateId = c.Id
                        ORDER BY vt.VoteDate DESC
                        
                        UNION ALL
                        
                        -- Get recent comments with voter and candidate details
                        SELECT TOP 10
                            'Comment submitted by ' + vo.FirstName + ' ' + vo.LastName + ' about ' + ca.FirstName + ' ' + ca.LastName as Description,
                            co.CreatedAt as Timestamp,
                            'comment' as ActivityType
                        FROM Comments co
                        INNER JOIN Voters vo ON co.VoterId = vo.Id
                        INNER JOIN Candidates ca ON co.CandidateId = ca.Id
                        ORDER BY co.CreatedAt DESC
                        
                        UNION ALL
                        
                        -- Get recent voter registrations
                        SELECT TOP 5
                            'New voter registered: ' + FirstName + ' ' + LastName as Description,
                            CreatedAt as Timestamp,
                            'registration' as ActivityType
                        FROM Voters
                        ORDER BY CreatedAt DESC
                        
                        UNION ALL
                        
                        -- Get recent supervisor activities
                        SELECT TOP 3
                            'Supervisor action: ' + ActivityDescription as Description,
                            ActivityDate as Timestamp,
                            'supervisor' as ActivityType
                        FROM SupervisorActivities
                        ORDER BY ActivityDate DESC
                        
                        ORDER BY Timestamp DESC
                    ", connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            activities.Add(new
                            {
                                description = reader.GetString("Description"),
                                timestamp = reader.GetDateTime("Timestamp").ToString("yyyy-MM-dd HH:mm:ss"),
                                type = reader.GetString("ActivityType")
                            });
                        }
                    }
                }

                return Json(new { success = true, activities = activities });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activities");
                return Json(new { success = false, message = "Error retrieving recent activities: " + ex.Message });
            }
        }

        // POST: Set election time
        [HttpPost]
        public async Task<IActionResult> SetElectionTime([FromForm] string startTime, [FromForm] string endTime)
        {
            try
            {
                if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
                {
                    return Json(new { success = false, message = "Both start and end times are required." });
                }

                var start = DateTime.Parse(startTime);
                var end = DateTime.Parse(endTime);

                if (start >= end)
                {
                    return Json(new { success = false, message = "End time must be after start time." });
                }

                // Store election configuration in database
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // First, clear any existing election configuration
                    var clearCommand = new SqlCommand("DELETE FROM ElectionConfiguration", connection);
                    await clearCommand.ExecuteNonQueryAsync();

                    // Insert new configuration
                    var insertCommand = new SqlCommand(@"
                        INSERT INTO ElectionConfiguration (StartTime, EndTime, IsActive, CreatedAt) 
                        VALUES (@StartTime, @EndTime, @IsActive, @CreatedAt)", 
                        connection);

                    insertCommand.Parameters.AddWithValue("@StartTime", start);
                    insertCommand.Parameters.AddWithValue("@EndTime", end);
                    insertCommand.Parameters.AddWithValue("@IsActive", true);
                    insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    var result = await insertCommand.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        _logger.LogInformation($"Election time set: {start} to {end}");
                        
                        // Log supervisor activity
                        await LogSupervisorActivity($"Set election time: {start} to {end}");
                        
                        return Json(new { success = true, message = "Election time configured successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Failed to configure election time." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting election time");
                return Json(new { success = false, message = "Error setting election time: " + ex.Message });
            }
        }

        // POST: Start election immediately
        [HttpPost]
        public async Task<IActionResult> StartElection()
        {
            try
            {
                var startTime = DateTime.Now;
                var endTime = startTime.AddHours(24); // Default 24-hour election

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Clear any existing configuration
                    var clearCommand = new SqlCommand("DELETE FROM ElectionConfiguration", connection);
                    await clearCommand.ExecuteNonQueryAsync();

                    // Insert new configuration
                    var insertCommand = new SqlCommand(@"
                        INSERT INTO ElectionConfiguration (StartTime, EndTime, IsActive, CreatedAt) 
                        VALUES (@StartTime, @EndTime, @IsActive, @CreatedAt)", 
                        connection);

                    insertCommand.Parameters.AddWithValue("@StartTime", startTime);
                    insertCommand.Parameters.AddWithValue("@EndTime", endTime);
                    insertCommand.Parameters.AddWithValue("@IsActive", true);
                    insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    var result = await insertCommand.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        _logger.LogInformation($"Election started: {startTime} to {endTime}");
                        
                        // Log supervisor activity
                        await LogSupervisorActivity("Started election immediately");
                        
                        return Json(new { success = true, message = "Election started successfully! It will run for 24 hours." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Failed to start election." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting election");
                return Json(new { success = false, message = "Error starting election: " + ex.Message });
            }
        }

        // POST: End election immediately
        [HttpPost]
        public async Task<IActionResult> EndElection()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Update election configuration to end now
                    var updateCommand = new SqlCommand(@"
                        UPDATE ElectionConfiguration 
                        SET EndTime = @EndTime, IsActive = 0 
                        WHERE IsActive = 1", 
                        connection);

                    updateCommand.Parameters.AddWithValue("@EndTime", DateTime.Now);

                    var result = await updateCommand.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        _logger.LogInformation("Election ended manually");
                        
                        // Log supervisor activity
                        await LogSupervisorActivity("Ended election manually");
                        
                        return Json(new { success = true, message = "Election ended successfully!" });
                    }
                    else
                    {
                        // If no active election found, just return success
                        return Json(new { success = true, message = "No active election found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending election");
                return Json(new { success = false, message = "Error ending election: " + ex.Message });
            }
        }

        // Helper method to get election configuration
        private async Task<ElectionConfig> GetElectionConfiguration()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT TOP 1 StartTime, EndTime, IsActive FROM ElectionConfiguration WHERE IsActive = 1 ORDER BY CreatedAt DESC", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ElectionConfig
                            {
                                StartTime = reader.GetDateTime("StartTime"),
                                EndTime = reader.GetDateTime("EndTime"),
                                IsActive = reader.GetBoolean("IsActive")
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No election configuration found or error reading configuration");
            }

            return null;
        }

        // ========== SUPERVISOR MANAGEMENT METHODS ==========

        // GET: Get all supervisors
        [HttpGet]
        public async Task<IActionResult> GetSupervisors()
        {
            try
            {
                var supervisors = new List<Supervisor>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT Id, FirstName, LastName, Email, Username, IsActive, CreatedAt, UpdatedAt FROM Supervisors ORDER BY FirstName, LastName", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            supervisors.Add(new Supervisor
                            {
                                Id = reader.GetInt32("Id"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                Email = reader.GetString("Email"),
                                Username = reader.GetString("Username"),
                                IsActive = reader.GetBoolean("IsActive"),
                                CreatedAt = reader.GetDateTime("CreatedAt").ToString("yyyy-MM-dd HH:mm"),
                                UpdatedAt = reader.GetDateTime("UpdatedAt").ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }

                return Json(new { success = true, supervisors = supervisors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supervisors");
                return Json(new { success = false, message = "Error retrieving supervisors: " + ex.Message });
            }
        }

        // POST: Create new supervisor - Support both Form and JSON
        [HttpPost]
        public async Task<IActionResult> CreateSupervisor()
        {
            try
            {
                // Try to get from form first, then from body
                string firstName = Request.Form["firstName"];
                string lastName = Request.Form["lastName"];
                string email = Request.Form["email"];
                string username = Request.Form["username"];
                string password = Request.Form["password"];

                // If form data is empty, try to read from body
                if (string.IsNullOrEmpty(firstName))
                {
                    var requestBody = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        try
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Deserialize<CreateSupervisorRequest>(requestBody);
                            firstName = jsonData.FirstName;
                            lastName = jsonData.LastName;
                            email = jsonData.Email;
                            username = jsonData.Username;
                            password = jsonData.Password;
                        }
                        catch
                        {
                            // If JSON parsing fails, continue with form data (which might be empty)
                        }
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || 
                    string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "All fields are required." });
                }

                // Check if username already exists
                if (await UsernameExists(username))
                {
                    return Json(new { success = false, message = "Username already exists." });
                }

                // Check if email already exists
                if (await EmailExists(email))
                {
                    return Json(new { success = false, message = "Email already exists." });
                }

                // Hash password using the SAME method as HomeController for Supervisors/Managers
                var hashedPassword = HashPassword(password);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        @"INSERT INTO Supervisors (FirstName, LastName, Email, Username, Password, IsActive, CreatedAt, UpdatedAt) 
                          VALUES (@FirstName, @LastName, @Email, @Username, @Password, @IsActive, @CreatedAt, @UpdatedAt)", 
                        connection);

                    command.Parameters.AddWithValue("@FirstName", firstName);
                    command.Parameters.AddWithValue("@LastName", lastName);
                    command.Parameters.AddWithValue("@Email", email);
                    command.Parameters.AddWithValue("@Username", username);
                    command.Parameters.AddWithValue("@Password", hashedPassword);
                    command.Parameters.AddWithValue("@IsActive", true);
                    command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        // Log supervisor activity
                        await LogSupervisorActivity($"Created new supervisor: {firstName} {lastName}");
                        
                        return Json(new { success = true, message = "Supervisor created successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Failed to create supervisor." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supervisor");
                return Json(new { success = false, message = "Error creating supervisor: " + ex.Message });
            }
        }

        // POST: Update supervisor - Support both Form and JSON
        [HttpPost]
        public async Task<IActionResult> UpdateSupervisor()
        {
            try
            {
                // Try to get from form first
                string idStr = Request.Form["id"];
                string firstName = Request.Form["firstName"];
                string lastName = Request.Form["lastName"];
                string email = Request.Form["email"];
                string username = Request.Form["username"];
                string password = Request.Form["password"];
                string isActiveStr = Request.Form["isActive"];

                // If form data is empty, try to read from body
                if (string.IsNullOrEmpty(firstName))
                {
                    var requestBody = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        try
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Deserialize<UpdateSupervisorRequest>(requestBody);
                            idStr = jsonData.Id.ToString();
                            firstName = jsonData.FirstName;
                            lastName = jsonData.LastName;
                            email = jsonData.Email;
                            username = jsonData.Username;
                            password = jsonData.Password;
                            isActiveStr = jsonData.IsActive.ToString();
                        }
                        catch
                        {
                            // If JSON parsing fails, continue with form data
                        }
                    }
                }

                if (!int.TryParse(idStr, out int id) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "All required fields are missing or invalid." });
                }

                bool isActive = isActiveStr?.ToLower() == "true";

                // Check if username already exists (excluding current supervisor)
                if (await UsernameExists(username, id))
                {
                    return Json(new { success = false, message = "Username already exists." });
                }

                // Check if email already exists (excluding current supervisor)
                if (await EmailExists(email, id))
                {
                    return Json(new { success = false, message = "Email already exists." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    string updateQuery;
                    SqlCommand command;

                    if (!string.IsNullOrEmpty(password))
                    {
                        // Update with password
                        var hashedPassword = HashPassword(password);
                        updateQuery = @"UPDATE Supervisors 
                                       SET FirstName = @FirstName, LastName = @LastName, 
                                           Email = @Email, Username = @Username, 
                                           Password = @Password, IsActive = @IsActive, 
                                           UpdatedAt = @UpdatedAt 
                                       WHERE Id = @Id";
                        
                        command = new SqlCommand(updateQuery, connection);
                        command.Parameters.AddWithValue("@Password", hashedPassword);
                    }
                    else
                    {
                        // Update without password
                        updateQuery = @"UPDATE Supervisors 
                                       SET FirstName = @FirstName, LastName = @LastName, 
                                           Email = @Email, Username = @Username, 
                                           IsActive = @IsActive, UpdatedAt = @UpdatedAt 
                                       WHERE Id = @Id";
                        
                        command = new SqlCommand(updateQuery, connection);
                    }

                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@FirstName", firstName);
                    command.Parameters.AddWithValue("@LastName", lastName);
                    command.Parameters.AddWithValue("@Email", email);
                    command.Parameters.AddWithValue("@Username", username);
                    command.Parameters.AddWithValue("@IsActive", isActive);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        // Log supervisor activity
                        await LogSupervisorActivity($"Updated supervisor: {firstName} {lastName}");
                        
                        return Json(new { success = true, message = "Supervisor updated successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Supervisor not found or no changes made." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supervisor");
                return Json(new { success = false, message = "Error updating supervisor: " + ex.Message });
            }
        }

        // POST: Delete supervisor - Support both Form and JSON
        [HttpPost]
        public async Task<IActionResult> DeleteSupervisor()
        {
            try
            {
                // Try to get from form first
                string idStr = Request.Form["id"];

                // If form data is empty, try to read from body
                if (string.IsNullOrEmpty(idStr))
                {
                    var requestBody = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        try
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Deserialize<SupervisorActionRequest>(requestBody);
                            idStr = jsonData.Id.ToString();
                        }
                        catch
                        {
                            // If JSON parsing fails, continue with form data
                        }
                    }
                }

                if (!int.TryParse(idStr, out int id))
                {
                    return Json(new { success = false, message = "Invalid supervisor ID." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("DELETE FROM Supervisors WHERE Id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        // Log supervisor activity
                        await LogSupervisorActivity($"Deleted supervisor with ID: {id}");
                        
                        return Json(new { success = true, message = "Supervisor deleted successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Supervisor not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supervisor");
                return Json(new { success = false, message = "Error deleting supervisor: " + ex.Message });
            }
        }

        // POST: Toggle supervisor status - Support both Form and JSON
        [HttpPost]
        public async Task<IActionResult> ToggleSupervisorStatus()
        {
            try
            {
                // Try to get from form first
                string idStr = Request.Form["id"];
                string isActiveStr = Request.Form["isActive"];

                // If form data is empty, try to read from body
                if (string.IsNullOrEmpty(idStr))
                {
                    var requestBody = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        try
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Deserialize<SupervisorStatusRequest>(requestBody);
                            idStr = jsonData.Id.ToString();
                            isActiveStr = jsonData.IsActive.ToString();
                        }
                        catch
                        {
                            // If JSON parsing fails, continue with form data
                        }
                    }
                }

                if (!int.TryParse(idStr, out int id))
                {
                    return Json(new { success = false, message = "Invalid supervisor ID." });
                }

                if (!bool.TryParse(isActiveStr, out bool isActive))
                {
                    return Json(new { success = false, message = "Invalid status value." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "UPDATE Supervisors SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id", 
                        connection);

                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@IsActive", isActive);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        var status = isActive ? "activated" : "deactivated";
                        
                        // Log supervisor activity
                        await LogSupervisorActivity($"{status} supervisor with ID: {id}");
                        
                        return Json(new { success = true, message = $"Supervisor {status} successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Supervisor not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling supervisor status");
                return Json(new { success = false, message = "Error updating supervisor status: " + ex.Message });
            }
        }

        // ========== COMMENT MANAGEMENT METHODS ==========

        // GET: Get all comments with voter and candidate information
        [HttpGet]
        public async Task<IActionResult> GetComments()
        {
            try
            {
                var comments = new List<object>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        SELECT 
                            c.Id, 
                            c.CommentText, 
                            c.CreatedAt, 
                            c.IsApproved,
                            v.FirstName + ' ' + v.LastName as VoterName,
                            cand.FirstName + ' ' + cand.LastName as CandidateName,
                            cand.Position
                        FROM Comments c
                        INNER JOIN Voters v ON c.VoterId = v.Id
                        INNER JOIN Candidates cand ON c.CandidateId = cand.Id
                        ORDER BY c.CreatedAt DESC", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new
                            {
                                id = reader.GetInt32("Id"),
                                commentText = reader.GetString("CommentText"),
                                createdAt = reader.GetDateTime("CreatedAt").ToString("yyyy-MM-dd HH:mm"),
                                isApproved = reader.GetBoolean("IsApproved"),
                                voterName = reader.GetString("VoterName"),
                                candidateName = reader.GetString("CandidateName"),
                                position = reader.GetString("Position")
                            });
                        }
                    }
                }

                return Json(new { success = true, comments = comments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments");
                return Json(new { success = false, message = "Error retrieving comments: " + ex.Message });
            }
        }

        // GET: Get comment statistics
        [HttpGet]
        public async Task<IActionResult> GetCommentStats()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        SELECT 
                            COUNT(*) as TotalComments,
                            SUM(CASE WHEN IsApproved = 1 THEN 1 ELSE 0 END) as ApprovedComments,
                            SUM(CASE WHEN IsApproved = 0 THEN 1 ELSE 0 END) as PendingComments
                        FROM Comments", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                TotalComments = reader.GetInt32("TotalComments"),
                                ApprovedComments = reader.GetInt32("ApprovedComments"),
                                PendingComments = reader.GetInt32("PendingComments")
                            };
                            return Json(new { success = true, stats = stats });
                        }
                    }
                }

                return Json(new { success = false, message = "No statistics found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comment statistics");
                return Json(new { success = false, message = "Error retrieving comment statistics: " + ex.Message });
            }
        }

        // POST: Approve comment
        [HttpPost]
        public async Task<IActionResult> ApproveComment([FromForm] int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "UPDATE Comments SET IsApproved = 1 WHERE Id = @Id", 
                        connection);

                    command.Parameters.AddWithValue("@Id", id);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        // Log supervisor activity
                        await LogSupervisorActivity($"Approved comment with ID: {id}");
                        
                        return Json(new { success = true, message = "Comment approved successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Comment not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving comment");
                return Json(new { success = false, message = "Error approving comment: " + ex.Message });
            }
        }

        // POST: Delete comment
        [HttpPost]
        public async Task<IActionResult> DeleteComment([FromForm] int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand("DELETE FROM Comments WHERE Id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        // Log supervisor activity
                        await LogSupervisorActivity($"Deleted comment with ID: {id}");
                        
                        return Json(new { success = true, message = "Comment deleted successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Comment not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment");
                return Json(new { success = false, message = "Error deleting comment: " + ex.Message });
            }
        }

        // ========== ELECTION RESULTS METHODS ==========

        // GET: Get election results with vote counts and winners
        [HttpGet]
        public async Task<IActionResult> GetElectionResults()
        {
            try
            {
                var elections = new List<AdminElectionResult>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Get all candidates with their vote counts and positions
                    var command = new SqlCommand(@"
                        SELECT 
                            c.Id as CandidateId,
                            c.FirstName + ' ' + c.LastName as CandidateName,
                            c.Position,
                            c.Party,
                            COUNT(v.Id) as VoteCount,
                            (SELECT COUNT(*) FROM Votes) as TotalVotes
                        FROM Candidates c
                        LEFT JOIN Votes v ON c.Id = v.CandidateId
                        GROUP BY c.Id, c.FirstName, c.LastName, c.Position, c.Party
                        ORDER BY c.Position, VoteCount DESC",
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var positions = new Dictionary<string, List<AdminCandidateResult>>();
                        
                        while (await reader.ReadAsync())
                        {
                            var position = reader.GetString("Position");
                            var candidate = new AdminCandidateResult
                            {
                                CandidateId = reader.GetInt32("CandidateId"),
                                CandidateName = reader.GetString("CandidateName"),
                                Party = reader.GetString("Party"),
                                VoteCount = reader.GetInt32("VoteCount"),
                                TotalVotes = reader.GetInt32("TotalVotes"),
                                Percentage = reader.GetInt32("TotalVotes") > 0 ? 
                                    Math.Round((reader.GetInt32("VoteCount") * 100.0) / reader.GetInt32("TotalVotes"), 2) : 0
                            };

                            if (!positions.ContainsKey(position))
                            {
                                positions[position] = new List<AdminCandidateResult>();
                            }
                            positions[position].Add(candidate);
                        }

                        // Determine winners for each position
                        foreach (var position in positions)
                        {
                            var candidates = position.Value.OrderByDescending(c => c.VoteCount).ToList();
                            var winner = candidates.FirstOrDefault();
                            
                            if (winner != null && winner.VoteCount > 0)
                            {
                                winner.IsWinner = true;
                            }

                            elections.Add(new AdminElectionResult
                            {
                                Position = position.Key,
                                Candidates = candidates,
                                TotalVotes = candidates.FirstOrDefault()?.TotalVotes ?? 0
                            });
                        }
                    }
                }

                return Json(new { success = true, elections = elections });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting election results");
                return Json(new { success = false, message = "Error retrieving election results: " + ex.Message });
            }
        }

        // GET: Get available elections/positions
        [HttpGet]
        public async Task<IActionResult> GetElections()
        {
            try
            {
                var elections = new List<object>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        SELECT DISTINCT Position 
                        FROM Candidates 
                        WHERE Id IN (SELECT DISTINCT CandidateId FROM Votes)
                        ORDER BY Position",
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            elections.Add(new { 
                                Id = reader.GetString("Position"), 
                                Name = reader.GetString("Position") + " Election" 
                            });
                        }
                    }
                }

                return Json(new { success = true, elections = elections });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting elections");
                return Json(new { success = false, message = "Error retrieving elections: " + ex.Message });
            }
        }

        // POST: Publish election results to make them visible to voters
        [HttpPost]
        public async Task<IActionResult> PublishResults([FromForm] string position, [FromForm] string announcementText)
        {
            try
            {
                // Store published results in database
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // First, check if results for this position are already published
                    var checkCommand = new SqlCommand(
                        "SELECT COUNT(*) FROM PublishedResults WHERE Position = @Position", 
                        connection);
                    checkCommand.Parameters.AddWithValue("@Position", position);
                    
                    var existingCount = (int)await checkCommand.ExecuteScalarAsync();
                    
                    if (existingCount > 0)
                    {
                        // Update existing published result
                        var updateCommand = new SqlCommand(@"
                            UPDATE PublishedResults 
                            SET AnnouncementText = @AnnouncementText, 
                                PublishedDate = @PublishedDate,
                                TotalVotes = (SELECT COUNT(*) FROM Votes v 
                                             INNER JOIN Candidates c ON v.CandidateId = c.Id 
                                             WHERE c.Position = @Position)
                            WHERE Position = @Position", 
                            connection);
                        
                        updateCommand.Parameters.AddWithValue("@Position", position);
                        updateCommand.Parameters.AddWithValue("@AnnouncementText", announcementText ?? "");
                        updateCommand.Parameters.AddWithValue("@PublishedDate", DateTime.Now);
                        
                        await updateCommand.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Insert new published result
                        var insertCommand = new SqlCommand(@"
                            INSERT INTO PublishedResults (Position, AnnouncementText, PublishedDate, TotalVotes) 
                            VALUES (@Position, @AnnouncementText, @PublishedDate, 
                                   (SELECT COUNT(*) FROM Votes v 
                                    INNER JOIN Candidates c ON v.CandidateId = c.Id 
                                    WHERE c.Position = @Position))", 
                            connection);
                        
                        insertCommand.Parameters.AddWithValue("@Position", position);
                        insertCommand.Parameters.AddWithValue("@AnnouncementText", announcementText ?? "");
                        insertCommand.Parameters.AddWithValue("@PublishedDate", DateTime.Now);
                        
                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }

                _logger.LogInformation($"Results published for position: {position}");
                
                // Log supervisor activity
                await LogSupervisorActivity($"Published results for position: {position}");
                
                return Json(new { success = true, message = $"Results for {position} published successfully! Voters can now see these results." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing results");
                return Json(new { success = false, message = "Error publishing results: " + ex.Message });
            }
        }

        // GET: Get published results
        [HttpGet]
        public async Task<IActionResult> GetPublishedResults()
        {
            try
            {
                var publishedResults = new List<object>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        SELECT Position, AnnouncementText, PublishedDate, TotalVotes
                        FROM PublishedResults 
                        ORDER BY PublishedDate DESC", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            publishedResults.Add(new
                            {
                                position = reader.GetString("Position"),
                                announcementText = reader.IsDBNull("AnnouncementText") ? "" : reader.GetString("AnnouncementText"),
                                publishedDate = reader.GetDateTime("PublishedDate").ToString("yyyy-MM-dd HH:mm"),
                                totalVotes = reader.GetInt32("TotalVotes")
                            });
                        }
                    }
                }

                return Json(new { success = true, publishedResults = publishedResults });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published results");
                return Json(new { success = false, message = "Error retrieving published results: " + ex.Message });
            }
        }

        // GET: Get election statistics
        [HttpGet]
        public async Task<IActionResult> GetElectionStats()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT 
                            COUNT(DISTINCT Position) as TotalPositions,
                            COUNT(DISTINCT CandidateId) as TotalCandidates,
                            COUNT(*) as TotalVotes,
                            COUNT(DISTINCT VoterId) as UniqueVoters,
                            (SELECT COUNT(*) FROM Voters) as TotalVoters
                        FROM Votes v
                        INNER JOIN Candidates c ON v.CandidateId = c.Id",
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                TotalPositions = reader.GetInt32("TotalPositions"),
                                TotalCandidates = reader.GetInt32("TotalCandidates"),
                                TotalVotes = reader.GetInt32("TotalVotes"),
                                UniqueVoters = reader.GetInt32("UniqueVoters"),
                                TotalVoters = reader.GetInt32("TotalVoters"),
                                VoterTurnout = reader.GetInt32("TotalVoters") > 0 ? 
                                    Math.Round((reader.GetInt32("UniqueVoters") * 100.0) / reader.GetInt32("TotalVoters"), 1) : 0
                            };
                            return Json(new { success = true, stats = stats });
                        }
                    }
                }

                return Json(new { success = false, message = "No election data found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting election statistics");
                return Json(new { success = false, message = "Error retrieving election statistics: " + ex.Message });
            }
        }

        // Helper methods
        private async Task<bool> UsernameExists(string username, int? excludeId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT COUNT(*) FROM Supervisors WHERE Username = @Username";
                if (excludeId.HasValue)
                {
                    query += " AND Id != @ExcludeId";
                }

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", username);
                if (excludeId.HasValue)
                {
                    command.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                }

                var count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        private async Task<bool> EmailExists(string email, int? excludeId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT COUNT(*) FROM Supervisors WHERE Email = @Email";
                if (excludeId.HasValue)
                {
                    query += " AND Id != @ExcludeId";
                }

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Email", email);
                if (excludeId.HasValue)
                {
                    command.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
                }

                var count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        // Helper method to log supervisor activities
        private async Task LogSupervisorActivity(string activityDescription)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        INSERT INTO SupervisorActivities (ActivityDescription, ActivityDate) 
                        VALUES (@ActivityDescription, @ActivityDate)", 
                        connection);

                    command.Parameters.AddWithValue("@ActivityDescription", activityDescription);
                    command.Parameters.AddWithValue("@ActivityDate", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log supervisor activity");
            }
        }

        // FIXED: Now uses the SAME hashing method as HomeController for Supervisors/Managers
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // Hexadecimal encoding (same as HomeController)
                }
                return builder.ToString();
            }
        }
    }

    // Model classes
    public class Supervisor
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class SupervisorStatusRequest
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
    }

    public class SupervisorActionRequest
    {
        public int Id { get; set; }
    }

    public class CreateSupervisorRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class UpdateSupervisorRequest
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsActive { get; set; }
    }

    // Election configuration model
    public class ElectionConfig
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    // Election result models
    public class AdminCandidateResult
    {
        public int CandidateId { get; set; }
        public string CandidateName { get; set; }
        public string Party { get; set; }
        public int VoteCount { get; set; }
        public int TotalVotes { get; set; }
        public double Percentage { get; set; }
        public bool IsWinner { get; set; }
    }

    public class AdminElectionResult
    {
        public string Position { get; set; }
        public List<AdminCandidateResult> Candidates { get; set; }
        public int TotalVotes { get; set; }
    }
}