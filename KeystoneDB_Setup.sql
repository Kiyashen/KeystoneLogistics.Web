USE KeystoneLogisticsDB;
GO

-- 1. DROP EXISTING TABLES IN FOREIGN KEY ORDER
DROP TABLE IF EXISTS dbo.AuditLogs;
DROP TABLE IF EXISTS dbo.PODDocuments;
DROP TABLE IF EXISTS dbo.Loads;
DROP TABLE IF EXISTS dbo.Vehicles;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.Drivers;
DROP TABLE IF EXISTS dbo.Customers;
GO

-- 2. RECREATE ALL TABLES WITH LECTURER REQUIREMENTS
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

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Password NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL, -- 'Admin', 'Customer', 'Driver'
    Email NVARCHAR(100) NULL
);

CREATE TABLE Vehicles (
    VehicleId INT IDENTITY(1,1) PRIMARY KEY,
    VehicleName NVARCHAR(50) NOT NULL,
    CapacityKg DECIMAL(10,2) NOT NULL,
    IsAvailable BIT DEFAULT 1,
    CurrentLocation NVARCHAR(100) DEFAULT 'Durban Central Hub'
);

CREATE TABLE Loads (
    LoadId INT IDENTITY(1,1) PRIMARY KEY,
    TrackingNumber NVARCHAR(50) UNIQUE NOT NULL,
    CustomerId INT FOREIGN KEY REFERENCES Customers(CustomerId),
    DriverId INT NULL FOREIGN KEY REFERENCES Drivers(DriverId),
    AssignedVehicleId INT NULL FOREIGN KEY REFERENCES Vehicles(VehicleId),
    PickupLocation NVARCHAR(255) NOT NULL,
    DropoffLocation NVARCHAR(255) NOT NULL,
    CargoDescription NVARCHAR(255) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending',
    WorkStatus NVARCHAR(20) DEFAULT 'Pending', -- 'Pending', 'Accepted', 'Rejected'
    RejectionReason NVARCHAR(250) NULL,
    CollectionPasscode NVARCHAR(10) NULL, -- 4-digit pickup PIN
    RouteSafetyRating NVARCHAR(20) DEFAULT 'Safe', -- 'Safe', 'Caution', 'High Risk'
    CurrentLocation NVARCHAR(100) DEFAULT 'Pickup Point',
    IsCollected BIT DEFAULT 0,
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

INSERT INTO Users (Username, Password, Role, Email)
VALUES 
('admin', 'admin123', 'Admin', 'admin@keystonelogistics.co.za'),
('driver1', 'driver123', 'Driver', 'm.naidoo@keystonelogistics.co.za'),
('customer1', 'cust123', 'Customer', 'm.khumalo@apexfreight.co.za');

INSERT INTO Vehicles (VehicleName, CapacityKg, IsAvailable, CurrentLocation)
VALUES 
('Van 1 - Light Express (1.5 Tonne)', 1500.00, 1, 'Durban Central Hub'),
('Van 2 - Heavy Freight (3.5 Tonne)', 3500.00, 1, 'Durban Central Hub');


INSERT INTO Customers (CompanyName, ContactPerson, Email, Phone)
VALUES
('Apex Freight SA', 'Mandla Khumalo', 'm.khumalo@apexfreight.co.za', '031 301 4455'),
('Zululand Distributing', 'Johan van der Merwe', 'johan@zululanddist.co.za', '035 789 1234'),
('KZN Coastline Logistics', 'Thandiwe Mthembu', 'tmthembu@kzncoastline.co.za', '031 562 9988'),
('DSV Logistics SA', 'Farhaan Patel', 'f.patel@dsv.co.za', '031 205 7711');

INSERT INTO Drivers (FullName, Phone, VehicleRegistration, IsAvailable)
VALUES
('Mahil Naidoo', '082 411 9201', 'NDS 741 ZN', 1),
('David Botha', '083 652 1098', 'NDW 882 ZN', 1),
('Lungelo Ndlovu', '084 330 8712', 'NDL 402 ZN', 0);


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

INSERT INTO Loads (TrackingNumber, CustomerId, DriverId, AssignedVehicleId, PickupLocation, DropoffLocation, CargoDescription, Status, WorkStatus, CollectionPasscode, RouteSafetyRating, CurrentLocation)
VALUES
('KL-2026-001', 1, 1, 1, 'Maydon Wharf Gate 4', 'Hammarsdale Industrial Park', 'Auto Parts & Components', 'Dispatched', 'Accepted', '4821', 'Safe', 'N3 Highway - Pinetown'),
('KL-2026-002', 2, 2, 2, 'Richards Bay Dry Bulk Terminal', 'New Germany Warehouse', 'Industrial Steel Coils', 'En Route', 'Accepted', '1904', 'Caution', 'En Route to Pickup'),
('KL-2026-003', 4, NULL, NULL, 'Takealot Durban Hub', 'Umhlanga Ridge', 'Consumer Electronics', 'Pending', 'Pending', '7392', 'Safe', 'Takealot Hub');
GO

-- 4. VERIFY TABLES
SELECT * FROM Users;
SELECT * FROM Vehicles;
SELECT * FROM Loads;

