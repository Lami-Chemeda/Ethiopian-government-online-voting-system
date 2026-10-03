CREATE DATABASE VotingDB;
GO

USE VotingDB;
GO

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
    
    CONSTRAINT CHK_Voter_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Voter_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Voter_Password_Length CHECK (LEN(Password) >= 6)
);

CREATE UNIQUE INDEX IX_Voters_PhoneNumber ON voters(PhoneNumber);
CREATE INDEX IX_Voters_NationalId ON voters(NationalId);

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
    
    CONSTRAINT CHK_Admin_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Admin_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Admin_Password_Length CHECK (LEN(Password) >= 6)
);

INSERT INTO Admins (NationalId, Nationality, Region, PhoneNumber, FirstName, MiddleName, LastName, Age, Sex, Password)
VALUES ('123456789012345', 'Ethiopian', 'Addis Ababa', '0912345678', 'Abebe', 'Kebede', 'Alemu', 30, 'Male', 'Admin@123');

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
    
    CONSTRAINT CHK_Supervisor_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Supervisor_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Supervisor_Password_Length CHECK (LEN(Password) >= 6)
);

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
    
    CONSTRAINT CHK_Manager_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Manager_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Manager_Password_Length CHECK (LEN(Password) >= 6)
);

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
    SymbolName NVARCHAR(100) NOT NULL DEFAULT 'Lion',
    SymbolImagePath NVARCHAR(500) NULL,
    SymbolUnicode NVARCHAR(20) NOT NULL DEFAULT '🦁',
    PartyColor NVARCHAR(20) NOT NULL DEFAULT '#1d3557',
    
    CONSTRAINT CHK_Candidate_Age CHECK (Age >= 18 AND Age <= 120),
    CONSTRAINT CHK_Candidate_Sex CHECK (Sex IN ('Male', 'Female')),
    CONSTRAINT CHK_Candidate_Password_Length CHECK (LEN(Password) >= 6)
);

CREATE TABLE Votes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VoterNationalId NVARCHAR(50) NOT NULL,
    CandidateNationalId NVARCHAR(50) NOT NULL,
    VoteDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    IPAddress NVARCHAR(45),
    
    CONSTRAINT FK_Votes_Voters FOREIGN KEY (VoterNationalId) REFERENCES Voters(NationalId),
    CONSTRAINT FK_Votes_Candidates FOREIGN KEY (CandidateNationalId) REFERENCES Candidates(NationalId),
    CONSTRAINT UQ_Votes_Voter UNIQUE (VoterNationalId)
);

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
    
    CONSTRAINT CHK_ElectionSettings_DateRange CHECK (EndDate > StartDate),
    CONSTRAINT CHK_ElectionSettings_Region CHECK (Region IN (
        'All Regions', 'Addis Ababa', 'Oromia', 'Amhara', 'Tigray', 
        'Somali', 'Afar', 'Dire Dawa', 'Benishangul-Gumuz', 
        'Gambela', 'Harari', 'Southern Nations'
    ))
);

INSERT INTO ElectionSettings (ElectionName, StartDate, EndDate, IsActive, Region)
VALUES ('Ethiopian National Election 2024', DATEADD(DAY, 1, GETDATE()), DATEADD(DAY, 2, GETDATE()), 0, 'All Regions');

CREATE TABLE SystemActivityLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NationalId NVARCHAR(50) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Action NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    IpAddress NVARCHAR(50),
    UserAgent NVARCHAR(500),
    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
    Status NVARCHAR(50) NOT NULL,
    AdditionalData NVARCHAR(1000) NULL,
    
    CONSTRAINT CHK_SystemActivityLogs_Role CHECK (Role IN ('Voter', 'Admin', 'Supervisor', 'Manager', 'Candidate', 'System')),
    CONSTRAINT CHK_SystemActivityLogs_Status CHECK (Status IN ('Success', 'Failed', 'Attempt', 'Warning'))
);

CREATE INDEX IX_SystemActivityLogs_Timestamp ON SystemActivityLogs(Timestamp DESC);
CREATE INDEX IX_SystemActivityLogs_NationalId ON SystemActivityLogs(NationalId);
CREATE INDEX IX_SystemActivityLogs_Role ON SystemActivityLogs(Role);
CREATE INDEX IX_SystemActivityLogs_Status ON SystemActivityLogs(Status);

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
    
    CONSTRAINT CHK_SecurityAlerts_Severity CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low'))
);

CREATE INDEX IX_SecurityAlerts_AlertDate ON SecurityAlerts(AlertDate DESC);
CREATE INDEX IX_SecurityAlerts_Severity ON SecurityAlerts(Severity);
CREATE INDEX IX_SecurityAlerts_IsResolved ON SecurityAlerts(IsResolved);
CREATE INDEX IX_SecurityAlerts_NationalId ON SecurityAlerts(NationalId);
GO
