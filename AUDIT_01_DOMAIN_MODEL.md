# AUDIT_01_DOMAIN_MODEL.md

## Domain / Database Forensics

This document investigates the database structure, business entities, relationships, and data integrity within the Keystone Logistics system.

---

### SQL Schema Overview

**Database Setup File**:
- File: `KeystoneDB_Setup.sql`

#### Tables Created

| Table          | Purpose                                                                 |
|----------------|-------------------------------------------------------------------------|
| `Customers`    | Stores B2C/B2B customer details, including company info and contacts. |
| `Drivers`      | Represents drivers, including availability, registration numbers.     |
| `Users`        | Manages all system users (admins, customers, drivers).                |
| `Vehicles`     | Tracks delivery vehicles, their capacity, and availability.           |
| `Loads`        | Main business unit representing shipments, including route/logistics |
| `PODDocuments` | Tracks proof-of-delivery files attached to jobs.                      |
| `AuditLogs`    | Logs operational actions with timestamps and performer information.   |

#### Relationships and Foreign Keys

| Entity         | Relationships                          |
|----------------|---------------------------------------|
| `Loads`        | `CustomerId` → `Customers(CustomerId)`|
|                | `DriverId` → `Drivers(DriverId)`      |
|                | `AssignedVehicleId` → `Vehicles(VehicleId)`|
| `PODDocuments` | `LoadId` → `Loads(LoadId)`            |
| `AuditLogs`    | `LoadId` → `Loads(LoadId)`            |


---

### Entity Descriptions
#### `Customers`:
- **Purpose**: Stores B2C/B2B customer profiles, company associations.
- **Fields**: `CustomerId` (PK), `CompanyName`, `Email`, `Phone`.
- **Business Meaning**: Differentiates individual user vs companies.

#### `Drivers`:
- **Purpose**: Stores drivers, operational state.
- **Key Fields**: `IsAvailable` realistic turn-like pickup assignments.

#### `Vehicles`
..Est Big Mechrospection Incl Route Exit Safety metrics etc bodies es