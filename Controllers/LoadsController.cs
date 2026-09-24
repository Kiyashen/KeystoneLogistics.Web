using System;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using KeystoneLogistics.Models;
using KeystoneLogistics.Services;

namespace KeystoneLogistics.Controllers
{
    public class LoadsController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();
        private const string AdminEmail = "keyram.smma.18@gmail.com";

        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                string targetEmail = (!string.IsNullOrWhiteSpace(toEmail) && !toEmail.Contains("kzncoastline.co.za"))
                    ? toEmail
                    : ConfigurationManager.AppSettings["FallbackEmail"] ?? AdminEmail;
                string host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                int port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;
                string senderEmail = ConfigurationManager.AppSettings["SmtpUser"] ?? AdminEmail;
                string senderPassword = ConfigurationManager.AppSettings["SmtpPass"] ?? "mkkpkkmxdleikmjb";
                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(senderEmail, "Keystone Logistics");
                        mailMessage.To.Add(targetEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;
                        client.Send(mailMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Email dispatch error: " + ex.Message);
            }
        }

        private string Clip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= 90 ? text : text.Substring(0, 90);
        }

        private string CustomerName(Load load)
        {
            if (load == null || !load.CustomerId.HasValue) return "Customer";
            var c = load.Customer ?? db.Customers.Find(load.CustomerId.Value);
            if (c == null) return "Customer";
            return string.IsNullOrWhiteSpace(c.CompanyName) ? (c.ContactPerson ?? "Customer") : c.CompanyName;
        }

        private string DriverName(Load load)
        {
            if (load == null || !load.DriverId.HasValue) return "Driver";
            var d = load.Driver ?? db.Drivers.Find(load.DriverId.Value);
            return d != null && !string.IsNullOrWhiteSpace(d.FullName) ? d.FullName : "Driver";
        }

        private void SaveSafe()
        {
            try { db.SaveChanges(); }
            catch (DbEntityValidationException vex)
            {
                string details = "";
                foreach (var eve in vex.EntityValidationErrors)
                    foreach (var err in eve.ValidationErrors)
                        details += err.PropertyName + ": " + err.ErrorMessage + " | ";
                throw new Exception(details, vex);
            }
        }

        private int JobsCompletedToday(int? driverId)
        {
            if (!driverId.HasValue) return 0;
            DateTime start = DateTime.Today;
            DateTime end = start.AddDays(1);
            return db.Loads.Count(l =>
                l.DriverId == driverId &&
                l.DeliveredDate.HasValue &&
                l.DeliveredDate.Value >= start &&
                l.DeliveredDate.Value < end);
        }

        public ActionResult Index()
        {
            if (Session["UserRole"] == null) return RedirectToAction("Login", "Account");
            string userRole = Session["UserRole"]?.ToString();
            int userId = Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id) ? id : 0;
            var loads = db.Loads.Include(l => l.Customer).Include(l => l.Driver).Include(l => l.Vehicle).AsQueryable();
            if (userRole == "Customer") loads = loads.Where(l => l.CustomerId == userId);
            else if (userRole == "Driver") loads = loads.Where(l => l.DriverId == userId || l.WorkStatus == "Accepted");
            ViewBag.AvailableVehicles = db.Vehicles.ToList();
            ViewBag.AvailableDrivers = db.Drivers.ToList();
            ViewBag.AuditLogs = db.AuditLogs.Include(a => a.Load).OrderByDescending(a => a.Timestamp).ToList();
            return View(loads.ToList());
        }

        public ActionResult Details(int id)
        {
            var load = db.Loads.Include(l => l.Vehicle).Include(l => l.Driver).FirstOrDefault(l => l.LoadId == id);
            if (load == null) return HttpNotFound();
            ViewBag.PODs = db.PODDocuments.Where(p => p.LoadId == id).ToList();
            return View(load);
        }

        public ActionResult Create()
        {
            if (Session["UserRole"]?.ToString() != "Customer") return RedirectToAction("Index");
            try { ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName"); }
            catch { ViewBag.CustomerList = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text"); }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "PickupLocation,DropoffLocation,CargoDescription")] Load load)
        {
            if (Session["UserRole"]?.ToString() != "Customer") return RedirectToAction("Index");
            if (ModelState.IsValid)
            {
                int count = db.Loads.Count() + 1;
                load.TrackingNumber = $"KL-2026-{count:D3}";
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sessionUserId))
                    load.CustomerId = sessionUserId;
                else
                {
                    var defaultCustomer = db.Customers.FirstOrDefault();
                    load.CustomerId = defaultCustomer != null ? defaultCustomer.CustomerId : 1;
                }
                load.Status = "Pending";
                load.WorkStatus = "Pending";
                load.RouteSafetyRating = "Safe";
                load.CurrentLocation = load.PickupLocation;
                db.Loads.Add(load);
                SaveSafe();
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = load.LoadId,
                    Action = Clip("Created " + load.TrackingNumber + " for " + CustomerName(load)),
                    PerformedBy = "Customer",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
                string subject = "New Shipment Created: " + load.TrackingNumber;
                string body = "<h2>KEYSTONE LOGISTICS</h2><p>Customer: " + CustomerName(load) + "</p>"
                    + "<p>Tracking: " + load.TrackingNumber + "</p>"
                    + "<p>Pickup: " + load.PickupLocation + "</p>"
                    + "<p>Dropoff: " + load.DropoffLocation + "</p>";
                SendEmail(AdminEmail, subject, body);
                TempData["SuccessMessage"] = "Work request created successfully! Tracking Number: " + load.TrackingNumber;
                return RedirectToAction("Index");
            }
            try { ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName"); }
            catch { ViewBag.CustomerList = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text"); }
            return View(load);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptRequest(int id, int driverId, string routeSafety, int? vehicleId)
        {
            if (Session["UserRole"]?.ToString() != "Admin") return RedirectToAction("Index");
            var load = db.Loads.Include(l => l.Customer).FirstOrDefault(l => l.LoadId == id);
            if (load == null) return RedirectToAction("Index");
            var driver = db.Drivers.Find(driverId);
            if (driver == null)
            {
                TempData["ErrorMessage"] = "Please select a valid driver.";
                return RedirectToAction("Index");
            }

            bool driverBusy = db.Loads.Any(l =>
                l.DriverId == driverId &&
                l.LoadId != id &&
                (l.Status == "En Route" || l.Status == "Dispatched"));

            load.WorkStatus = "Accepted";
            load.DriverId = driverId;
            load.RouteSafetyRating = string.IsNullOrEmpty(routeSafety) ? "Safe" : routeSafety;
            load.Status = driverBusy ? "Queued" : "Dispatched";
            load.DispatchedDate = DateTime.Now;
            if (string.IsNullOrEmpty(load.CollectionPasscode))
                load.CollectionPasscode = new Random().Next(1000, 9999).ToString();

            string cust = CustomerName(load);
            string auditAction = driverBusy
                ? ("Queued " + load.TrackingNumber + " " + driver.FullName + "/" + cust)
                : ("Assigned " + load.TrackingNumber + " " + driver.FullName + "/" + cust);

            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip(auditAction),
                PerformedBy = "Admin",
                Timestamp = DateTime.Now
            });

            try { SaveSafe(); }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Could not assign driver. " + ex.Message;
                return RedirectToAction("Index");
            }

            if (driverBusy)
            {
                SendEmail(AdminEmail, "Queued " + load.TrackingNumber,
                    "<p>Driver " + driver.FullName + " is mid-route.</p><p>Customer: " + cust + "</p><p>Next pickup: " + load.PickupLocation + "</p><p>PIN: " + load.CollectionPasscode + "</p>");
                TempData["SuccessMessage"] = driver.FullName + " is on the road. " + load.TrackingNumber + " queued for " + cust + ".";
            }
            else
            {
                SendEmail(AdminEmail, "Dispatched " + load.TrackingNumber,
                    "<p>Driver: " + driver.FullName + "</p><p>Customer: " + cust + "</p><p>PIN: " + load.CollectionPasscode + "</p>");
                TempData["SuccessMessage"] = "Accepted. Driver: " + driver.FullName + ". Customer: " + cust + ". PIN: " + load.CollectionPasscode;
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int id, string rejectionReason)
        {
            if (Session["UserRole"]?.ToString() != "Admin") return RedirectToAction("Index");
            var load = db.Loads.Find(id);
            if (load != null)
            {
                load.WorkStatus = "Rejected";
                load.RejectionReason = rejectionReason;
                load.Status = "Cancelled";
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = id,
                    Action = Clip("Rejected " + load.TrackingNumber + " " + CustomerName(load)),
                    PerformedBy = "Admin",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
                TempData["ErrorMessage"] = "Work Request #" + load.TrackingNumber + " Rejected.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyCollection(int id, string enteredPasscode)
        {
            if (Session["UserRole"]?.ToString() != "Driver") return RedirectToAction("Index");
            var load = db.Loads.Include(l => l.Customer).Include(l => l.Driver).FirstOrDefault(l => l.LoadId == id);
            if (load != null)
            {
                string logAction;
                if (load.CollectionPasscode == enteredPasscode)
                {
                    DateTime departed = DateTime.Now;
                    load.IsCollected = true;
                    load.Status = "En Route";
                    load.CurrentLocation = "In Transit";
                    load.DispatchedDate = departed;
                    logAction = "DEPART " + departed.ToString("HH:mm") + " " + load.TrackingNumber + " " + DriverName(load) + " > " + CustomerName(load);
                    TempData["SuccessMessage"] = "PIN verified. Departure " + departed.ToString("HH:mm:ss");
                }
                else
                {
                    logAction = "Bad PIN " + load.TrackingNumber + " " + CustomerName(load);
                    TempData["ErrorMessage"] = "Incorrect Collection PIN.";
                }
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = id,
                    Action = Clip(logAction),
                    PerformedBy = "Driver",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkDelivered(int id, string scannedQRCode)
        {
            if (Session["UserRole"]?.ToString() != "Driver") return RedirectToAction("Index");
            var load = db.Loads.Include(l => l.Customer).Include(l => l.Driver).FirstOrDefault(l => l.LoadId == id);
            if (load == null)
            {
                TempData["ErrorMessage"] = "Load record not found.";
                return RedirectToAction("Index");
            }
            if (!string.IsNullOrEmpty(scannedQRCode) &&
                !scannedQRCode.Equals(load.TrackingNumber, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Invalid tracking number.";
                return RedirectToAction("Index");
            }

            DateTime arrived = DateTime.Now;
            load.Status = "Delivered";
            load.WorkStatus = "Completed";
            load.CurrentLocation = load.DropoffLocation;
            load.DeliveredDate = arrived;
            if (load.AssignedVehicleId != null)
            {
                var vehicle = db.Vehicles.Find(load.AssignedVehicleId);
                if (vehicle != null) vehicle.IsAvailable = true;
            }

            int packagesToday = JobsCompletedToday(load.DriverId) + 1;
            string cust = CustomerName(load);
            string drv = DriverName(load);

            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip("ARRIVE " + arrived.ToString("HH:mm") + " " + load.TrackingNumber + " " + drv + ">" + cust + " pkgs=" + packagesToday),
                PerformedBy = "Driver",
                Timestamp = DateTime.Now
            });

            Load nextJob = null;
            if (load.DriverId.HasValue)
            {
                nextJob = db.Loads.Include(l => l.Customer)
                    .Where(l => l.DriverId == load.DriverId && l.Status == "Queued")
                    .OrderBy(l => l.LoadId)
                    .FirstOrDefault();
                if (nextJob != null)
                {
                    nextJob.Status = "Dispatched";
                    nextJob.DispatchedDate = DateTime.Now;
                    db.AuditLogs.Add(new AuditLog
                    {
                        LoadId = nextJob.LoadId,
                        Action = Clip("Next " + nextJob.TrackingNumber + " " + CustomerName(nextJob) + " after " + load.TrackingNumber),
                        PerformedBy = "System",
                        Timestamp = DateTime.Now
                    });
                }
            }

            SaveSafe();
            SendEmail(AdminEmail, "Delivered " + load.TrackingNumber,
                "<p>Customer: " + cust + "</p><p>Driver: " + drv + "</p><p>Arrival: " + arrived.ToString("yyyy-MM-dd HH:mm:ss") + "</p><p>Packages today: " + packagesToday + "</p>");

            if (nextJob != null)
            {
                SendEmail(AdminEmail, "Next job ready: " + nextJob.TrackingNumber,
                    "<p>Customer: " + CustomerName(nextJob) + "</p><p>Pickup: " + nextJob.PickupLocation + "</p><p>PIN: " + nextJob.CollectionPasscode + "</p>");
                TempData["SuccessMessage"] = load.TrackingNumber + " delivered for " + cust + ". Next: " + nextJob.TrackingNumber;
            }
            else
            {
                TempData["SuccessMessage"] = load.TrackingNumber + " delivered for " + cust + ". Packages today: " + packagesToday;
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadPOD(int LoadId, HttpPostedFileBase podFile, string notes)
        {
            if (podFile == null || podFile.ContentLength == 0)
            {
                TempData["PODError"] = "Please select a file to upload.";
                return RedirectToAction("Details", new { id = LoadId });
            }
            try
            {
                var podService = new PODService();
                string virtualPath = podService.SavePODFile(podFile);
                if (string.IsNullOrEmpty(virtualPath))
                {
                    TempData["PODError"] = "File upload failed.";
                    return RedirectToAction("Details", new { id = LoadId });
                }
                db.PODDocuments.Add(new PODDocument
                {
                    LoadId = LoadId,
                    FilePath = virtualPath,
                    Notes = notes ?? string.Empty
                });
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = LoadId,
                    Action = Clip("POD uploaded"),
                    PerformedBy = Session["UserRole"]?.ToString() ?? "System",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
                TempData["PODSuccess"] = "Proof of Delivery uploaded successfully!";
            }
            catch (Exception ex)
            {
                TempData["PODError"] = ex.Message;
            }
            return RedirectToAction("Details", new { id = LoadId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}