using System;
using System.Collections.Generic;
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
        private List<string> SavedPlaces()
        {
            int userId = Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id) ? id : 0;
            var mine = db.Loads.Where(l => userId == 0 || l.CustomerId == userId);
            return mine.Select(l => l.PickupLocation)
                .Concat(mine.Select(l => l.DropoffLocation))
                .Where(s => s != null && s != "")
                .Distinct()
                .OrderBy(s => s)
                .Take(20)
                .ToList();
        }
        private string ShortName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            var parts = displayName.Split(',');
            string shortName = parts.Length > 1 ? parts[0].Trim() + ", " + parts[1].Trim() : parts[0].Trim();
            return shortName.Length > 80 ? shortName.Substring(0, 80) : shortName;
        }
        private string SuggestPlace(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] tries = { text + ", KwaZulu-Natal, South Africa", text + ", South Africa", text };
            foreach (string query in tries)
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    string url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=za&q=" +
                                 Uri.EscapeDataString(query);
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "KeystoneLogistics/1.0 (student project)");
                        string json = client.DownloadString(url);
                        var nameMatch = Regex.Match(json, "\"display_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (!nameMatch.Success) continue;
                        return ShortName(nameMatch.Groups[1].Value);
                    }
                }
                catch { }
            }
            return null;
        }
        private bool NeedsSuggestion(string typed, out string town)
        {
            town = null;
            string raw = (typed ?? "").Trim();
            if (raw.Length == 0 || raw.Contains(",") || Regex.IsMatch(raw, @"\d")) return false;
            string lower = raw.ToLowerInvariant();
            string[] towns = { "pietermaritzburg", "pmb", "durban", "pinetown", "umhlanga", "chatsworth", "westville", "hillcrest", "ballito", "howick", "amanzimtoti", "phoenix", "verulam" };
            string[] ok = { "hub", "terminal", "depot", "warehouse", "centre", "center", "yard", "port", "distribution", "road", "street", "rd", "st", "blvd", "avenue", "ave", "drive", "industrial", "container", "central", "south", "north", "east", "west", "bank", "ridge" };
            foreach (string t in towns)
            {
                if (!lower.Contains(t)) continue;
                town = t == "pmb" ? "Pietermaritzburg" : char.ToUpper(t[0]) + t.Substring(1);
                string rest = Regex.Replace(lower, t, " ");
                rest = Regex.Replace(rest, @"[^a-z]", " ");
                foreach (string word in rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Length <= 1) continue;
                    bool allowed = false;
                    foreach (string a in ok) if (word == a) allowed = true;
                    if (!allowed) return true;
                }
            }
            return false;
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
            string cleaned = Regex.Replace(text, @"\b(hub|terminal|depot|warehouse|centre|center|yard|port|distribution)\b", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            var queries = new List<string> { text + ", South Africa", text };
            if (!string.IsNullOrWhiteSpace(cleaned)) queries.Add(cleaned + ", South Africa");
            string lower = text.ToLowerInvariant();
            string[] towns = { "pietermaritzburg", "pmb", "durban", "pinetown", "umhlanga", "chatsworth", "westville", "hillcrest", "ballito", "howick", "amanzimtoti", "phoenix", "verulam" };
            foreach (string town in towns)
            {
                if (lower.Contains(town))
                    queries.Add((town == "pmb" ? "Pietermaritzburg" : town) + ", South Africa");
            }
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
            string from = (pickup ?? "").Trim();
            string to = (dropoff ?? "").Trim();
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                error = "Pickup and drop-off cannot be the same place.";
                return false;
            }
            if (NeedsSuggestion(from, out string pickupTown))
            {
                error = "Could not match that pickup. Did you mean: " + pickupTown + "? Type that and submit again.";
                return false;
            }
            if (NeedsSuggestion(to, out string dropoffTown))
            {
                error = "Could not match that drop-off. Did you mean: " + dropoffTown + "? Type that and submit again.";
                return false;
            }
            if (!TryLocate(pickup, out double pLat, out double pLng))
            {
                string hint = SuggestPlace(pickup);
                error = string.IsNullOrEmpty(hint)
                    ? "Could not find that pickup on the map. Type the address again."
                    : "Could not match that pickup. Did you mean: " + hint + "?";
                return false;
            }
            if (!TryLocate(dropoff, out double dLat, out double dLng))
            {
                string hint = SuggestPlace(dropoff);
                error = string.IsNullOrEmpty(hint)
                    ? "Could not find that dropoff on the map. Type the address again."
                    : "Could not match that drop-off. Did you mean: " + hint + "?";
                return false;
            }
            if (Math.Abs(pLat - dLat) < 0.01 && Math.Abs(pLng - dLng) < 0.01)
            {
                error = "Pickup and drop-off cannot be the same place.";
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
        private double RoadKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLng = (lng2 - lng1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)) * 1.35;
        }
        private DateTime EstimateArrival(Load load, DateTime departed)
        {
            if (TryLocate(load.PickupLocation, out double pLat, out double pLng) &&
                TryLocate(load.DropoffLocation, out double dLat, out double dLng))
            {
                double km = RoadKm(pLat, pLng, dLat, dLng);
                double minutes = Math.Max(45, Math.Round(km / 45.0 * 60) + 15);
                return departed.AddMinutes(minutes);
            }
            return departed.AddHours(3);
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
        private void StampWeight(Load load, decimal kg)
        {
            string cargo = Regex.Replace(load.CargoDescription ?? "", @"\s*\|\s*WT:\d+(\.\d+)?", "");
            load.CargoDescription = cargo.Trim() + " | WT:" + kg.ToString("0.0", CultureInfo.InvariantCulture);
        }
        public ActionResult Index()
        {
            if (Session["UserRole"] == null) return RedirectToAction("Login", "Account");
            string userRole = Session["UserRole"]?.ToString();
            int userId = Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id) ? id : 0;
            var testLoad = db.Loads.Include(l => l.Customer)
                .Where(l => l.Status == "Delivered" && l.DeliveredDate.HasValue)
                .OrderByDescending(l => l.DeliveredDate)
                .FirstOrDefault();
            if (testLoad != null && !db.AuditLogs.Any(a => a.LoadId == testLoad.LoadId && a.Action.Contains("OVERDUE notice")))
            {
                string body = "<h2 style='color:#9B2C2C;'>PAYMENT OVERDUE. FINAL DEMAND.</h2>"
                    + "<p>Tracking: <strong>" + testLoad.TrackingNumber + "</strong></p>"
                    + "<p>Customer: " + CustomerName(testLoad) + "</p>"
                    + "<p>Route: " + testLoad.PickupLocation + " to " + testLoad.DropoffLocation + "</p>"
                    + "<p>Delivered: " + testLoad.DeliveredDate.Value.ToString("dd MMMM yyyy HH:mm") + "</p>"
                    + "<p><strong>The 3-day payment window has closed. This account is now overdue.</strong></p>"
                    + "<p>If this invoice is not settled immediately, Keystone Logistics will hand the account to its attorneys for recovery. That includes small claims court, collection costs, and a block on all future bookings.</p>"
                    + "<p>Log in and use Pay Now now.</p>";
                SendEmail(CustomerEmail(testLoad), "OVERDUE: final demand " + testLoad.TrackingNumber, body);
                SendEmail(AdminEmail, "OVERDUE: final demand " + testLoad.TrackingNumber, body);
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = testLoad.LoadId,
                    Action = Clip("OVERDUE notice " + testLoad.TrackingNumber),
                    PerformedBy = "System",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
                TempData["ErrorMessage"] = "Overdue demand emailed for " + testLoad.TrackingNumber + ".";
            }
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
            ViewBag.SavedPlaces = SavedPlaces();
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "PickupLocation,DropoffLocation,CargoDescription")] Load load)
        {
            if (Session["UserRole"]?.ToString() != "Customer") return RedirectToAction("Index");
            ViewBag.SavedPlaces = SavedPlaces();
            if (!RouteAllowed(load.PickupLocation, load.DropoffLocation, out string areaError))
            {
                TempData["ErrorMessage"] = areaError;
                try { ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName"); }
                catch { ViewBag.CustomerList = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text"); }
                return View(load);
            }
            if (ModelState.IsValid)
            {
                bool fragile = string.Equals(Request.Form["IsFragile"], "true", StringComparison.OrdinalIgnoreCase);
                string cargo = (load.CargoDescription ?? "").Trim();
                if (fragile && !cargo.StartsWith("FRAGILE:", StringComparison.OrdinalIgnoreCase))
                    cargo = "FRAGILE: " + cargo;
                load.CargoDescription = cargo;
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
                    Action = Clip((fragile ? "FRAGILE " : "Created ") + load.TrackingNumber + " for " + CustomerName(load)),
                    PerformedBy = "Customer",
                    Timestamp = DateTime.Now
                });
                SaveSafe();
                SendEmail(AdminEmail, (fragile ? "FRAGILE shipment: " : "New Shipment Created: ") + load.TrackingNumber,
                    "<h2>KEYSTONE LOGISTICS</h2><p>Customer: " + CustomerName(load) + "</p><p>Tracking: " + load.TrackingNumber + "</p><p>Pickup: " + load.PickupLocation + "</p><p>Dropoff: " + load.DropoffLocation + "</p><p>Cargo: " + load.CargoDescription + "</p>"
                    + (fragile ? "<p style='color:#9B2C2C;'><strong>FRAGILE. Handle with care.</strong></p>" : ""));
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
            bool fragile = (load.CargoDescription ?? "").StartsWith("FRAGILE:", StringComparison.OrdinalIgnoreCase);
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
            string fragileLine = fragile ? "<p style='color:#9B2C2C;'><strong>FRAGILE. Handle with care.</strong></p>" : "";
            if (driverBusy)
            {
                SendEmail(AdminEmail, "Queued " + load.TrackingNumber,
                    "<p>Driver " + driver.FullName + " is mid-route.</p><p>Customer: " + cust + "</p><p>Next pickup: " + load.PickupLocation + "</p><p>PIN: " + load.CollectionPasscode + "</p>" + fragileLine);
                TempData["SuccessMessage"] = driver.FullName + " is on the road. " + load.TrackingNumber + " queued for " + cust + ".";
            }
            else
            {
                SendEmail(AdminEmail, "Dispatched " + load.TrackingNumber,
                    "<p>Driver: " + driver.FullName + "</p><p>Customer: " + cust + "</p><p>PIN: " + load.CollectionPasscode + "</p>" + fragileLine);
                TempData["SuccessMessage"] = "Accepted. Driver: " + driver.FullName + ". Customer: " + cust + ". PIN: " + load.CollectionPasscode;
            }
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int id, string rejectionReason)
        {
            if (Session["UserRole"]?.ToString() != "Admin") return RedirectToAction("Index");
            var load = db.Loads.Include(l => l.Customer).FirstOrDefault(l => l.LoadId == id);
            if (load == null) return RedirectToAction("Index");
            string reason = string.IsNullOrWhiteSpace(rejectionReason) ? "Declined by dispatch" : rejectionReason.Trim();
            bool paid = load.Status == "Paid / Processing" || load.Status == "Delivered";
            load.WorkStatus = "Rejected";
            load.RejectionReason = reason;
            load.Status = "Cancelled";
            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip("Rejected " + load.TrackingNumber + " " + CustomerName(load)),
                PerformedBy = "Admin",
                Timestamp = DateTime.Now
            });
            SaveSafe();
            string body = "<h2>Shipment rejected</h2>"
                + "<p>Tracking: " + load.TrackingNumber + "</p>"
                + "<p>Customer: " + CustomerName(load) + "</p>"
                + "<p>Route: " + load.PickupLocation + " to " + load.DropoffLocation + "</p>"
                + "<p>Cargo: " + load.CargoDescription + "</p>"
                + "<p>Reason: " + reason + "</p>"
                + (paid
                    ? "<p><strong>This job was already paid. Refund the full amount from the PayFast dashboard.</strong></p>"
                    : "<p>This request was declined before payment. No refund is due.</p>");
            SendEmail(AdminEmail, "Rejected: " + load.TrackingNumber, body);
            string customerEmail = CustomerEmail(load);
            if (!string.Equals(customerEmail, AdminEmail, StringComparison.OrdinalIgnoreCase))
                SendEmail(customerEmail, "Rejected: " + load.TrackingNumber, body);
            TempData["ErrorMessage"] = "Work Request #" + load.TrackingNumber + " Rejected. Email sent.";
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyCollection(int id, string enteredPasscode, decimal actualWeightKg, HttpPostedFileBase pickupPhoto)
        {
            if (Session["UserRole"]?.ToString() != "Driver") return RedirectToAction("Index");
            var load = db.Loads.Include(l => l.Customer).Include(l => l.Driver).FirstOrDefault(l => l.LoadId == id);
            if (load != null)
            {
                string logAction;
                if (actualWeightKg <= 0)
                {
                    TempData["ErrorMessage"] = "Enter the actual weight in kilograms before verifying pickup.";
                    return RedirectToAction("Index");
                }
                if (load.CollectionPasscode == enteredPasscode)
                {
                    DateTime departed = DateTime.Now;
                    DateTime eta = EstimateArrival(load, departed);
                    decimal weightCharge = Math.Round(actualWeightKg * 1.50m, 2);
                    load.IsCollected = true;
                    load.Status = "En Route";
                    load.CurrentLocation = "In Transit";
                    load.DispatchedDate = departed;
                    StampWeight(load, actualWeightKg);
                    string photoPath = SaveDeliveryPhoto(pickupPhoto, load.TrackingNumber + "-pickup");
                    if (!string.IsNullOrEmpty(photoPath))
                    {
                        db.PODDocuments.Add(new PODDocument
                        {
                            LoadId = id,
                            FilePath = "/Content/DeliveryPhotos/" + Path.GetFileName(photoPath),
                            Notes = "Pickup photo"
                        });
                    }
                    logAction = "DEPART " + departed.ToString("HH:mm") + " WT " + actualWeightKg.ToString("0.0", CultureInfo.InvariantCulture) + "kg " + load.TrackingNumber;
                    bool fragile = (load.CargoDescription ?? "").IndexOf("fragile", StringComparison.OrdinalIgnoreCase) >= 0;
                    string body = "<h2>KEYSTONE LOGISTICS</h2>"
                        + "<p>Hi " + CustomerName(load) + ",</p>"
                        + "<p>Your package has been collected and is now <strong>on the way</strong>.</p>"
                        + "<p>Tracking: <strong>" + load.TrackingNumber + "</strong></p>"
                        + "<p>From: " + load.PickupLocation + "</p>"
                        + "<p>To: " + load.DropoffLocation + "</p>"
                        + "<p>Driver: " + DriverName(load) + "</p>"
                        + "<p>Collected at: " + departed.ToString("HH:mm") + "</p>"
                        + "<p>Actual weight: <strong>" + actualWeightKg.ToString("N1") + " kg</strong></p>"
                        + "<p>Weight charge: <strong>R " + weightCharge.ToString("N2") + "</strong> at R1.50 per kg. This is added to the final invoice.</p>"
                        + "<p><strong>Estimated delivery: " + eta.ToString("HH:mm") + " today.</strong></p>"
                        + (fragile ? "<p style='color:#9B2C2C;'><strong>FRAGILE. The driver has been told to handle this with care.</strong></p>" : "")
                        + "<p>This estimate is based on the route distance. Traffic can move it.</p>"
                        + "<p>You will get a final notice, with the doorstep photo, once it is delivered.</p>";
                    SendEmail(CustomerEmail(load), "Collected: " + load.TrackingNumber + " is on the way", body, photoPath);
                    SendEmail(AdminEmail, "Collected: " + load.TrackingNumber + " is on the way", body, photoPath);
                    TempData["SuccessMessage"] = "PIN verified. " + actualWeightKg.ToString("N1") + " kg emailed. ETA " + eta.ToString("HH:mm") + ".";
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
            DateTime due = arrived.AddDays(3);
            load.Status = "Delivered";
            load.WorkStatus = "Completed";
            load.CurrentLocation = load.DropoffLocation;
            load.DeliveredDate = arrived;
            if (load.AssignedVehicleId != null)
            {
                var vehicle = db.Vehicles.Find(load.AssignedVehicleId);
                if (vehicle != null) vehicle.IsAvailable = true;
            }
            string cust = CustomerName(load);
            string drv = DriverName(load);
            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip("ARRIVE " + arrived.ToString("HH:mm") + " " + load.TrackingNumber + " pay by " + due.ToString("dd MMM")),
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
            string body = "<h2 style='color:#9B2C2C;'>FINAL NOTICE: PAYMENT IS NOW DUE</h2>"
                + "<p>Tracking: <strong>" + load.TrackingNumber + "</strong></p>"
                + "<p>Customer: " + cust + "</p>"
                + "<p>Driver: " + drv + "</p>"
                + "<p>Dropoff: " + load.DropoffLocation + "</p>"
                + "<p>Delivered: " + arrived.ToString("yyyy-MM-dd HH:mm") + ". The doorstep photo is attached.</p>"
                + "<p><strong>Payment deadline: " + due.ToString("dd MMMM yyyy") + ".</strong></p>"
                + "<p>If this invoice is still unpaid after that date, Keystone Logistics will hand the account to its attorneys for recovery. That includes small claims court, collection costs, and a block on all future bookings.</p>"
                + "<p>Log in and use Pay Now before the deadline. Pay Now stays available after the date, but the account will already be marked overdue.</p>";
            SendEmail(CustomerEmail(load), "FINAL NOTICE: payment due " + load.TrackingNumber, body, photoPath);
            SendEmail(AdminEmail, "FINAL NOTICE: payment due " + load.TrackingNumber, body, photoPath);
            TempData["SuccessMessage"] = nextJob != null
                ? load.TrackingNumber + " delivered for " + cust + ". Payment due " + due.ToString("dd MMM") + ". Next: " + nextJob.TrackingNumber
                : load.TrackingNumber + " delivered for " + cust + ". Payment due " + due.ToString("dd MMM") + ".";
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RoadAlert(int id, string alertType)
        {
            if (Session["UserRole"]?.ToString() != "Driver") return RedirectToAction("Index");
            var allowed = new Dictionary<string, string>
            {
                { "traffic", "Heavy traffic" },
                { "blocked", "Road blocked" },
                { "construction", "Construction" },
                { "risk", "High-risk area, avoid" }
            };
            if (string.IsNullOrWhiteSpace(alertType) || !allowed.ContainsKey(alertType))
            {
                TempData["ErrorMessage"] = "Pick a road update.";
                return RedirectToAction("Index");
            }
            var load = db.Loads.Include(l => l.Driver).FirstOrDefault(l => l.LoadId == id);
            if (load == null) return RedirectToAction("Index");
            string who = Session["Username"]?.ToString() ?? DriverName(load);
            string label = allowed[alertType];
            string route = (load.PickupLocation ?? "") + " to " + (load.DropoffLocation ?? "");
            db.AuditLogs.Add(new AuditLog
            {
                LoadId = id,
                Action = Clip("ROAD " + label + " " + load.TrackingNumber),
                PerformedBy = who,
                Timestamp = DateTime.Now
            });
            SaveSafe();
            string body = "<h2 style='color:#9B2C2C;'>ROAD UPDATE FOR ALL DRIVERS</h2>"
                + "<p><strong>" + label + "</strong></p>"
                + "<p>Driver: " + who + "</p>"
                + "<p>Job: " + load.TrackingNumber + "</p>"
                + "<p>Route: " + route + "</p>"
                + "<p>Time: " + DateTime.Now.ToString("HH:mm") + "</p>"
                + "<p>This is the job corridor, not a single street pin.</p>";
            SendEmail(AdminEmail, "ROAD ALERT all drivers: " + label + " " + load.TrackingNumber, body);
            TempData["SuccessMessage"] = "Sent to all drivers: " + label + " on " + route + ".";
            return RedirectToAction("Index");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}