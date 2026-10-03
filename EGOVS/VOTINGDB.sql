use votingDB
CREATE TABLE voters (
    NationalId NVARCHAR(50) PRIMARY KEY,
    Nationality NVARCHAR(50) NOT NULL DEFAULT 'Ethiopian',
    Region NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(50) NOT NULL UNIQUE,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Sex NVARCHAR(20) NOT NULL,
    Literate NVARCHAR(50) NOT NULL DEFAULT 'Yes',
    Password NVARCHAR(255) NOT NULL,
    RegisterDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    QRCodeData NVARCHAR(500) NULL,
    VisualPIN NVARCHAR(50) NULL DEFAULT '🦁,☕,🌾,🏠',
    PrefersVisualLogin BIT NOT NULL DEFAULT 1,
    
    -- Check constraints
    CONSTRAINT CHK_Voter_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Voter_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Voter_Password_Length CHECK (LEN(Password) >= 6)
);

CREATE UNIQUE INDEX IX_Voters_PhoneNumber ON voters(PhoneNumber);
CREATE INDEX IX_Voters_NationalId ON voters(NationalId);
select * from voters
CREATE TABLE Admins (
    NationalId NVARCHAR(50) PRIMARY KEY,
    Nationality NVARCHAR(50) NOT NULL DEFAULT 'Ethiopian',
    Region NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Sex NVARCHAR(20) NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    
    -- Check constraints
    CONSTRAINT CHK_Admin_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Admin_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Admin_Password_Length CHECK (LEN(Password) >= 6)
);
INSERT INTO Admins (
    NationalId,
    Nationality,
    Region,
    PhoneNumber,
    FirstName,
    MiddleName,
    LastName,
    Age,
    Sex,
    Password
)
VALUES (
    '123456789012345',
    'Ethiopian',
    'Addis Ababa',
    '0912345678',
    'Abebe',
    'Kebede',
    'Alemu',
    30,
    'Male',
    'Admin@123'
);
DELETE FROM Admins
WHERE NationalId = '1234567890';
select* from admins
CREATE TABLE Supervisors (
    NationalId NVARCHAR(50) PRIMARY KEY,
    Nationality NVARCHAR(50) NOT NULL DEFAULT 'Ethiopian',
    Region NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Sex NVARCHAR(20) NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    Email NVARCHAR(100),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- Check constraints
    CONSTRAINT CHK_Supervisor_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Supervisor_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Supervisor_Password_Length CHECK (LEN(Password) >= 6)
);
select * from supervisors
CREATE TABLE Managers (
    NationalId NVARCHAR(50) PRIMARY KEY,
    Nationality NVARCHAR(50) NOT NULL DEFAULT 'Ethiopian',
    Region NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Sex NVARCHAR(20) NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    Email NVARCHAR(100),
    Username NVARCHAR(100),
    IsActive BIT NOT NULL DEFAULT 1,
    
    -- Check constraints
    CONSTRAINT CHK_Manager_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Manager_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Manager_Password_Length CHECK (LEN(Password) >= 6)
);
select * from managers
delete from managers where	NationalId=4265285312583492
CREATE TABLE Candidates (
    NationalId NVARCHAR(50) PRIMARY KEY,
    Nationality NVARCHAR(50) NOT NULL DEFAULT 'Ethiopian',
    Region NVARCHAR(100) NOT NULL,
    PhoneNumber NVARCHAR(50) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Sex NVARCHAR(20) NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    Party NVARCHAR(100) NOT NULL,
    Bio NVARCHAR(MAX),
    PhotoUrl NVARCHAR(500),
	 logo NVARCHAR(500),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- Check constraints
    CONSTRAINT CHK_Candidate_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Candidate_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Candidate_Password_Length CHECK (LEN(Password) >= 6)
);
ALTER TABLE Candidates 
ADD 
    SymbolName NVARCHAR(100) NOT NULL DEFAULT 'Lion',
    SymbolImagePath NVARCHAR(500) NULL,
    SymbolUnicode NVARCHAR(20) NOT NULL DEFAULT '🦁',
    PartyColor NVARCHAR(20) NOT NULL DEFAULT '#1d3557';
select * from candidates
select * from


delete from candidates where nationalid=5294706738075837
CREATE TABLE Votes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VoterNationalId NVARCHAR(50) NOT NULL,
    CandidateNationalId NVARCHAR(50) NOT NULL,
    VoteDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    IPAddress NVARCHAR(45),
    
    -- Foreign key constraints
    CONSTRAINT FK_Votes_Voters FOREIGN KEY (VoterNationalId) REFERENCES Voters(NationalId),
    CONSTRAINT FK_Votes_Candidates FOREIGN KEY (CandidateNationalId) REFERENCES Candidates(NationalId),
    
    -- Ensure one vote per voter
    CONSTRAINT UQ_Votes_Voter UNIQUE (VoterNationalId)
);
--
select * from supervisors

delete from votes where id=4
CREATE TABLE ResultPublishes (
    ResultId INT IDENTITY(1,1) PRIMARY KEY,
    CandidateNationalId NVARCHAR(50) NOT NULL,
    CandidateName NVARCHAR(300) NOT NULL,
    Party NVARCHAR(100) NOT NULL,
    VoteCount INT NOT NULL,
    Percentage DECIMAL(5,2) NOT NULL,
    IsWinner BIT NOT NULL DEFAULT 0,
    IsApproved BIT NOT NULL DEFAULT 0,
    ApprovedBy NVARCHAR(100),
    ApprovedDate DATETIME2,
    PublishedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT FK_ResultPublishes_Candidates FOREIGN KEY (CandidateNationalId) REFERENCES Candidates(NationalId)
);
select* from  ResultPublishes 
delete from ResultPublishes   where Resultid=10
select * from voters
select * from supervisors
delete from candidates where nationalid=5294706738075837
select * from candidates
select * from managers
select * from admins
select * from votes
delete from votes where id =19
select * from  ResultPublishes 
delete from ResultPublishes  where resultid= 8
-- Comments table with basic foreign key (if you prefer simplicity)
CREATE TABLE Comments (
		Id INT IDENTITY(1,1) PRIMARY KEY,
		Content NVARCHAR(1000) NOT NULL,
		SenderType NVARCHAR(20) NOT NULL,
		SenderNationalId NVARCHAR(50) NOT NULL,
		SenderName NVARCHAR(200),
		ReceiverType NVARCHAR(20) NOT NULL,
		ReceiverNationalId NVARCHAR(50),
		ReceiverName NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    CommentType NVARCHAR(50),
    IsRead BIT NOT NULL DEFAULT 0,
    Subject NVARCHAR(100) DEFAULT 'General Comment'
);
delete from comments where id=11


CREATE TABLE AdminLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NationalId NVARCHAR(50) NOT NULL,
    Action NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    IPAddress NVARCHAR(45),
    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
    Severity NVARCHAR(20) NOT NULL DEFAULT 'Info',
    
    CONSTRAINT FK_AdminLogs_Admins FOREIGN KEY (NationalId) REFERENCES Admins(NationalId)
);

-- Create ElectionSettings table
CREATE TABLE ElectionSettings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ElectionName NVARCHAR(200) NOT NULL DEFAULT 'Ethiopian National Election 2024',
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 0,
    Region NVARCHAR(100) NOT NULL DEFAULT 'All Regions',
    ResultsPublished BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- Insert default election settings
INSERT INTO ElectionSettings (ElectionName, StartDate, EndDate, IsActive, Region)
VALUES ('Ethiopian National Election 2024', DATEADD(DAY, 1, GETDATE()), DATEADD(DAY, 2, GETDATE()), 0, 'All Regions');

-- Create BackupLogs table

-- Create SystemActivityLogs table for tracking login attempts and user activities
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SystemActivityLogs' AND xtype='U')
BEGIN
    CREATE TABLE SystemActivityLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        NationalId NVARCHAR(50) NOT NULL,
        Role NVARCHAR(50) NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        IpAddress NVARCHAR(50),
        UserAgent NVARCHAR(500),
        Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
        Status NVARCHAR(50) NOT NULL, -- Success, Failed, Attempt
        AdditionalData NVARCHAR(1000) NULL,
        
        -- Constraint to ensure valid roles
        CONSTRAINT CHK_SystemActivityLogs_Role CHECK (Role IN ('Voter', 'Admin', 'Supervisor', 'Manager', 'Candidate', 'System')),
        CONSTRAINT CHK_SystemActivityLogs_Status CHECK (Status IN ('Success', 'Failed', 'Attempt', 'Warning'))
    )
END

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SystemActivityLogs_Timestamp')
BEGIN
    CREATE INDEX IX_SystemActivityLogs_Timestamp ON SystemActivityLogs(Timestamp DESC)
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SystemActivityLogs_NationalId')
BEGIN
    CREATE INDEX IX_SystemActivityLogs_NationalId ON SystemActivityLogs(NationalId)
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SystemActivityLogs_Role')
BEGIN
    CREATE INDEX IX_SystemActivityLogs_Role ON SystemActivityLogs(Role)
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SystemActivityLogs_Status')
BEGIN
    CREATE INDEX IX_SystemActivityLogs_Status ON SystemActivityLogs(Status)
END
select * from SystemActivityLogs
select * from comments
DELETE FROM SystemActivityLogs
WHERE id IN (210,211,212,213,214,215,216,217,218,219,220,221,222,223,224,225,226,227,228,229,230,231,232,233,234,235,236,237,238,239,240);
-- Create ElectionSettings table
CREATE TABLE ElectionSettings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ElectionName NVARCHAR(200) NOT NULL DEFAULT 'Ethiopian National Election 2024',
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 0,
    Region NVARCHAR(100) NOT NULL DEFAULT 'All Regions',
    ResultsPublished BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- Check constraints for data validation
    CONSTRAINT CHK_ElectionSettings_DateRange CHECK (EndDate > StartDate),
    CONSTRAINT CHK_ElectionSettings_Region CHECK (Region IN (
        'All Regions', 'Addis Ababa', 'Oromia', 'Amhara', 'Tigray', 
        'Somali', 'Afar', 'Dire Dawa', 'Benishangul-Gumuz', 
        'Gambela', 'Harari', 'Southern Nations'
    ))
); 
select * from  ElectionSettings
delete from ElectionSettings where id=5
select * from candidates
select * from supervisors
select * from voters
delete from voters where nationalid=6098045069547346
delete from voters where nationalId=2849706134790753
select * from managers
select * from ResultPublishes 
delete from votes  where id=13
select  *from votes
select * from admins


-- Create SecurityAlerts table for security monitoring and alerts
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SecurityAlerts' AND xtype='U')
BEGIN
    CREATE TABLE SecurityAlerts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AlertType NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        Severity NVARCHAR(20) NOT NULL CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low')),
        AlertDate DATETIME2 NOT NULL DEFAULT GETDATE(),
        IsResolved BIT NOT NULL DEFAULT 0,
        ResolvedBy NVARCHAR(50) NULL,
        ResolvedDate DATETIME2 NULL,
        AdditionalData NVARCHAR(1000) NULL,
        NationalId NVARCHAR(50) NULL,
        Role NVARCHAR(20) NULL,
        
        -- Constraint to ensure valid severity levels
        CONSTRAINT CHK_SecurityAlerts_Severity CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low'))
    )
    
    PRINT 'SecurityAlerts table created successfully.'
END
ELSE
BEGIN
    PRINT 'SecurityAlerts table already exists.'
END

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAlerts_AlertDate')
BEGIN
    CREATE INDEX IX_SecurityAlerts_AlertDate ON SecurityAlerts(AlertDate DESC)
    PRINT 'Index IX_SecurityAlerts_AlertDate created.'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAlerts_Severity')
BEGIN
    CREATE INDEX IX_SecurityAlerts_Severity ON SecurityAlerts(Severity)
    PRINT 'Index IX_SecurityAlerts_Severity created.'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAlerts_IsResolved')
BEGIN
    CREATE INDEX IX_SecurityAlerts_IsResolved ON SecurityAlerts(IsResolved)
    PRINT 'Index IX_SecurityAlerts_IsResolved created.'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAlerts_NationalId')
BEGIN
    CREATE INDEX IX_SecurityAlerts_NationalId ON SecurityAlerts(NationalId)
    PRINT 'Index IX_SecurityAlerts_NationalId created.'
END
select * from SecurityAlerts