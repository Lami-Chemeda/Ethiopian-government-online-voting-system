using System;

namespace VotingSystem.Models
{
    public class BackupFileInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string SizeFormatted { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public string DatabaseName { get; set; }
        public DateTime BackupDate { get; set; }
        public string BackupType { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
    }

    public class DeleteBackupRequest
    {
        public string FileName { get; set; }
    }

    public class RestoreBackupRequest
    {
        public string FileName { get; set; }
    }

    // REMOVED: SecurityAlert duplicate class
    // REMOVED: SystemActivityLog duplicate class

    public class ClearLogsRequest
    {
        public string ClearType { get; set; }
    }
}