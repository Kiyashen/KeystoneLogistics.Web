USE KeystoneLogisticsDB;
GO

-- 1. DROP OLD TABLES IN CORRECT FOREIGN KEY ORDER
DROP TABLE IF EXISTS dbo.AuditLogs;
DROP TABLE IF EXISTS dbo.PODDocuments;
DROP TABLE IF EXISTS dbo.Loads;
DROP TABLE IF EXISTS dbo.Drivers;
DROP TABLE IF EXISTS dbo.Customers;
GO

-- 2. RECREATE TABLES FROM SCRATCH
CREATE TABLE Customers (
    CustomerId INT IDENTITY(1,1) PRIMARY KEY,
    CompanyName NVARCHAR(100) NOT NULL,
    ContactPerson NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20) NOT NULL
);

CREATE TABLE Drivers (
    DriverId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    VehicleRegistration NVARCHAR(20) NOT NULL,
    IsAvailable BIT DEFAULT 1
);

CREATE TABLE Loads (
    LoadId INT IDENTITY(1,1) PRIMARY KEY,
    TrackingNumber NVARCHAR(50) UNIQUE NOT NULL,
    CustomerId INT FOREIGN KEY REFERENCES Customers(CustomerId),
    DriverId INT NULL FOREIGN KEY REFERENCES Drivers(DriverId),
    PickupLocation NVARCHAR(255) NOT NULL,
    DropoffLocation NVARCHAR(255) NOT NULL,
    CargoDescription NVARCHAR(255) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending',
    DispatchedDate DATETIME NULL,
    DeliveredDate DATETIME NULL
);

CREATE TABLE PODDocuments (
    PODId INT IDENTITY(1,1) PRIMARY KEY,
    LoadId INT FOREIGN KEY REFERENCES Loads(LoadId),
    FilePath NVARCHAR(500) NOT NULL,
    UploadedAt DATETIME DEFAULT GETDATE(),
    Notes NVARCHAR(255) NULL
);

CREATE TABLE AuditLogs (
    AuditId INT IDENTITY(1,1) PRIMARY KEY,
    LoadId INT FOREIGN KEY REFERENCES Loads(LoadId),
    Action NVARCHAR(100) NOT NULL,
    PerformedBy NVARCHAR(100) NOT NULL,
    Timestamp DATETIME DEFAULT GETDATE()
);
GO

-- 3. INSERT FRESH DUMMY DATA
INSERT INTO Customers (CompanyName, ContactPerson, Email, Phone) 
VALUES 
('Apex Freight SA', 'Mandla Khumalo', 'm.khumalo@apexfreight.co.za', '031 301 4455'),
('Zululand Distributing', 'Johan van der Merwe', 'johan@zululanddist.co.za', '035 789 1234'),
('KZN Coastline Logistics', 'Thandiwe Mthembu', 'tmthembu@kzncoastline.co.za', '031 562 9988'),
('Port Natal Trading', 'Farhaan Patel', 'f.patel@portnatal.co.za', '031 205 7711');

INSERT INTO Drivers (FullName, Phone, VehicleRegistration, IsAvailable) 
VALUES 
('Sipho Naidoo', '082 411 9201', 'KZN 741 GP', 1),
('David Botha', '083 652 1098', 'ND 88201', 1),
('Lungelo Ndlovu', '084 330 8712', 'NP 40291', 0);

INSERT INTO Loads (TrackingNumber, CustomerId, DriverId, PickupLocation, DropoffLocation, CargoDescription, Status)
VALUES 
('KL-2026-001', 1, 1, 'Maydon Wharf Gate 4', 'Hammarsdale Industrial Park', 'Auto Parts & Components', 'Dispatched'),
('KL-2026-002', 2, 2, 'Richards Bay Dry Bulk Terminal', 'New Germany Warehouse', 'Industrial Steel Coils', 'En Route'),
('KL-2026-003', 3, 3, 'Prospecton Manufacturing Hub', 'Cato Ridge Logistics Hub', 'FMCG & Packaging Materials', 'Delivered');
GO

-- 4. DISPLAY ALL VISUAL TABLES
SELECT * FROM Customers;
SELECT * FROM Drivers;
SELECT * FROM Loads;
SELECT * FROM PODDocuments;
SELECT * FROM AuditLogs;