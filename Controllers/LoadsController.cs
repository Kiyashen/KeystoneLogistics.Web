using System;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
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
        private const string AreaError = "We only accommodate deliveries between Durban and Pietermaritzburg and the surrounding areas.";

        private void SendEmail(string toEmail, string subject, string body, string attachmentPath = null)
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
                        if (!string.IsNullOrEmpty(attachmentPath) && System.IO.File.Exists(attachmentPath))
                            mailMessage.Attachments.Add(new Attachment(attachmentPath));
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

        private string CustomerEmail(Load load)
        {
            if (load == null || !load.CustomerId.HasValue) return AdminEmail;
            var c = load.Customer ?? db.Customers.Find(load.CustomerId.Value);
            return c != null && !string.IsNullOrWhiteSpace(c.Email) ? c.Email : AdminEmail;
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

        private bool InServiceArea(double lat, double lng)
        {
            return lat <= -29.35 && lat >= -30.20 && lng >= 29.95 && lng <= 31.25;
        }

        private bool TryLocate(string text, out double lat, out double lng)
        {
            lat = 0;
            lng = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var coord = Regex.Match(text, @"(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)");
            if (coord.Success &&
                double.TryParse(coord.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
                double.TryParse(coord.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lng))
            {
                return true;
            }
            string cleaned = Regex.Replace(text, @"\b(hub|terminal|depot|warehouse|centre|center|yard|port)\b", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            string[] queries = new[]
            {
                text + ", South Africa",
                string.IsNullOrWhiteSpace(cleaned) ? null : cleaned + ", South Africa",
                text
            };
            foreach (string query in queries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    string url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=za&q=" +
                                 Uri.EscapeDataString(query);
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "KeystoneLogistics/1.0 (student project)");
                        string json = client.DownloadString(url);
                        var latMatch = Regex.Match(json, "\"lat\"\\s*:\\s*\"(-?\\d+\\.\\d+)\"");
                        var lngMatch = Regex.Match(json, "\"lon\"\\s*:\\s*\"(-?\\d+\\.\\d+)\"");
                        if (!latMatch.Success || !lngMatch.Success) continue;
                        lat = double.Parse(latMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        lng = double.Parse(lngMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private bool RouteAllowed(string pickup, string dropoff, out string error)
        {
            error = null;
            if (!TryLocate(pickup, out double pLat, out double pLng))
            {
                error = "Could not find that pickup on the map. Type the address again.";
                return false;
            }
            if (!TryLocate(dropoff, out double dLat, out double dLng))
            {
                error = "Could not find that dropoff on the map. Type the address again.";
                return false;
            }
            if (!InServiceArea(pLat, pLng) || !InServiceArea(dLat, dLng))
            {
                error = AreaError;
                return false;
            }
            return true;
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

        private string SaveDeliveryPhoto(HttpPostedFileBase podPhoto, string trackingNumber)
        {
            if (podPhoto == null || podPhoto.ContentLength == 0) return null;
            string folder = Server.MapPath("~/Content/DeliveryPhotos");
            Directory.CreateDirectory(folder);
            string ext = Path.GetExtension(podPhoto.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            string fileName = trackingNumber + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext;
            string fullPath = Path.Combine(folder, fileName);
            podPhoto.SaveAs(fullPath);
            return fullPath;
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
            if (!RouteAllowed(load.PickupLocation, load.DropoffLocation, out string areaError))
            {
                TempData["ErrorMessage"] = areaError;
                try { ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName"); }
                catch { ViewBag.CustomerList = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text"); }
                return View(load);
            }
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
                SendEmail(AdminEmail, "New Shipment Created: " + load.TrackingNumber,
                    "<h2>KEYSTONE LOGISTICS</h2><p>Customer: " + CustomerName(load) + "</p><p>Tracking: " + load.TrackingNumber + "</p><p>Pickup: " + load.PickupLocation + "</p><p>Dropoff: " + load.DropoffLocation + "</p>");
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
            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip(driverBusy
                    ? "Queued " + load.TrackingNumber + " " + driver.FullName + "/" + cust
                    : "Assigned " + load.TrackingNumber + " " + driver.FullName + "/" + cust),
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
        public ActionResult MarkDelivered(int id, string scannedQRCode, HttpPostedFileBase podPhoto)
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
            if (podPhoto == null || podPhoto.ContentLength == 0)
            {
                TempData["ErrorMessage"] = "Snap a doorstep photo before confirming delivery.";
                return RedirectToAction("Index");
            }

            string photoPath = SaveDeliveryPhoto(podPhoto, load.TrackingNumber);
            db.PODDocuments.Add(new PODDocument
            {
                LoadId = id,
                FilePath = "/Content/DeliveryPhotos/" + Path.GetFileName(photoPath),
                Notes = "Doorstep photo"
            });

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
                Action = Clip("ARRIVE " + arrived.ToString("HH:mm") + " " + load.TrackingNumber + " " + drv + ">" + cust + " photo"),
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

            string body = "<h2>Your package has been delivered</h2>"
                + "<p>Tracking: " + load.TrackingNumber + "</p>"
                + "<p>Customer: " + cust + "</p>"
                + "<p>Driver: " + drv + "</p>"
                + "<p>Dropoff: " + load.DropoffLocation + "</p>"
                + "<p>Arrival: " + arrived.ToString("yyyy-MM-dd HH:mm:ss") + "</p>"
                + "<p>The doorstep photo is attached.</p>";
            SendEmail(CustomerEmail(load), "Delivered " + load.TrackingNumber, body, photoPath);
            SendEmail(AdminEmail, "Delivered " + load.TrackingNumber, body, photoPath);

            TempData["SuccessMessage"] = nextJob != null
                ? load.TrackingNumber + " delivered for " + cust + ". Photo emailed. Next: " + nextJob.TrackingNumber
                : load.TrackingNumber + " delivered for " + cust + ". Photo emailed.";
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