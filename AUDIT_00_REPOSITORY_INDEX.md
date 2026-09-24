# AUDIT_00_REPOSITORY_INDEX.md

## Repository Index

### General Index

- **KeystoneLogistics.sln**: Solution file.
- **KeystoneLogistics.csproj**: Project configuration.
- **Global.asax**/**Global.asax.cs**: Application lifecycle configuration.
- **Web.config**/**Web.Release.config**/**Web.Debug.config**: Web application settings.
- **KeystoneDB_Setup.sql**: Represents initial SQL schema setup.

### Codebase Inventory

#### Controllers
1. `AccountController.cs` (101 lines): Manages authentication and user account workflows.
2. `AuditLogsController.cs` (33 lines): Provides access to operation logs.
3. `CustomersController.cs` (142 lines): Manages customers (CRUD operations).
4. `DriversController.cs` (135 lines): Handles driver operations (availability, profiles).
5. `HomeController.cs` (143 lines): Static pages and routing.
6. `LoadsController.cs` (367 lines): Manages shipment and job operations.

#### Services
1. `AuditLogger.cs` (28 lines): Implements audit logging.
2. `ExportService.cs` (38 lines): Handles exporting of data.
3. `NotificationService.cs` (84 lines): Manages user notifications.
4. `PODService.cs` (40 lines): Handles proof of delivery processes.

#### Views
- **Account**: `Login.cshtml`, `ForgotPassword.cshtml`, `ResetPassword.cshtml`
- **Drivers**: `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Delete.cshtml`, `Details.cshtml`
- **Customers**: CRUD templates (`Index`, `Create`, `Edit`, `Delete`, `Details`)
- **Loads**: CRUD templates, `Details` (~similar mapping structure with varying lines).
- **Shared**: `_Layout.cshtml`, `Error.cshtml`

#### Scripts/Frontend Libraries
- JavaScript (`bootstrap.js`, `jquery.validate.js` + various mappings).
- CSS (`bootstrap-grid.css`, `Site.css`) defines style hierarchy.
- `Content/van.glb`: These imported external assets require possible geo/spatial outputs.

#### Dependencies/Binaries
- Libraries (`EntityFramework`, `QRCoder`, `Newtonsoft.Json`).
- `/bin` assemblies generated from builds.

#### SQL/Domains
- `KeystoneDB_Setup.sql`: Establish entities include drivers, loads core relationships/framework.
  Estimate: Partial representation only milestone `Keystone Init_Crud_Dtree.model` patterns remaining script defined later workflows create verification/hooks runtime support effects highlighted CoreSet prototype-depth.

---