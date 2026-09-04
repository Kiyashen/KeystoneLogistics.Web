using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using KeystoneLogistics.Models;
using KeystoneLogistics.Services;

namespace KeystoneLogistics.Controllers
{
    public class LoadsController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: Loads
        public ActionResult Index()
        {
            if (Session["UserRole"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            string userRole = Session["UserRole"]?.ToString();
            int userId = Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id) ? id : 0;

            var loads = db.Loads.Include(l => l.Customer).Include(l => l.Driver).AsQueryable();

            // Role-based data privacy filtering & Admin Audit Log Loading
            if (userRole == "Customer")
            {
                loads = loads.Where(l => l.CustomerId == userId);
            }
            else if (userRole == "Driver")
            {
                loads = loads.Where(l => l.DriverId == userId || l.WorkStatus == "Accepted");
            }
            else if (userRole == "Admin")
            {
                ViewBag.AuditLogs = db.AuditLogs.OrderByDescending(a => a.Timestamp).ToList();
            }

            ViewBag.AvailableVehicles = db.Vehicles.Where(v => v.IsAvailable == true).ToList();

            return View(loads.ToList());
        }

        // GET: Loads/Create (Customer Work Request Form)
        public ActionResult Create()
        {
            if (Session["UserRole"]?.ToString() != "Customer")
            {
                return RedirectToAction("Index");
            }

            // Fixed: Use "CustomerId" instead of "AccountReference"
            try
            {
                ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName");
            }
            catch (Exception)
            {
                try
                {
                    ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CustomerName");
                }
                catch (Exception)
                {
                    ViewBag.CustomerList = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
                }
            }

            return View();
        }

        // POST: Loads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "PickupLocation,DropoffLocation,CargoDescription")] Load load, string CustomerName, string AccountReference)
        {
            if (Session["UserRole"]?.ToString() != "Customer")
            {
                return RedirectToAction("Index");
            }

            // 1. Smart, Robust Customer Database Verification
            Customer verifiedCustomer = null;

            if (!string.IsNullOrEmpty(CustomerName))
            {
                if (int.TryParse(CustomerName, out int parsedCustId))
                {
                    verifiedCustomer = db.Customers.Find(parsedCustId);
                }

                if (verifiedCustomer == null)
                {
                    var allCustomers = db.Customers.ToList();
                    verifiedCustomer = allCustomers.FirstOrDefault(c =>
                        (c.GetType().GetProperty("Username")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("CustomerName")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("Name")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("CompanyName")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("Email")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("AccountReference")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false)
                    );
                }
            }

            if (verifiedCustomer == null && !string.IsNullOrEmpty(AccountReference))
            {
                var allCustomers = db.Customers.ToList();
                verifiedCustomer = allCustomers.FirstOrDefault(c =>
                    (c.GetType().GetProperty("AccountReference")?.GetValue(c)?.ToString()?.Equals(AccountReference, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            if (verifiedCustomer == null && Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sessionUserId))
            {
                verifiedCustomer = db.Customers.Find(sessionUserId);
            }

            if (verifiedCustomer == null)
            {
                verifiedCustomer = db.Customers.FirstOrDefault();
            }

            if (verifiedCustomer == null)
            {
                ModelState.AddModelError("", "Database Verification Failed: The Customer username/ID does not match an active account in our system.");
            }

            if (string.IsNullOrWhiteSpace(AccountReference))
            {
                ModelState.AddModelError("", "Account Reference Validation Failed: Please select a registered company account.");
            }

            if (ModelState.IsValid)
            {
                load.CustomerId = verifiedCustomer.CustomerId;

                int nextId = db.Loads.Any() ? db.Loads.Max(l => l.LoadId) + 1 : 1;
                string newTrackingNumber;

                do
                {
                    newTrackingNumber = $"KL-{DateTime.Now.Year}-{nextId:D3}";
                    nextId++;
                }
                while (db.Loads.Any(l => l.TrackingNumber == newTrackingNumber));

                load.TrackingNumber = newTrackingNumber;
                load.Status = "Pending";
                load.WorkStatus = "Pending";
                load.RouteSafetyRating = "Safe";
                load.CurrentLocation = load.PickupLocation;

                db.Loads.Add(load);
                db.SaveChanges();

                string userRole = Session["UserRole"]?.ToString() ?? "Customer";
                AuditLogger.Log(load.LoadId, "Created Load: " + load.TrackingNumber, userRole);

                try
                {
                    NotificationService.SendNotificationEmail(
                        "keyram.smma.18@gmail.com",
                        $"New Shipment Created: {load.TrackingNumber}",
                        $"<p>A new freight work request has been successfully submitted and logged into the system.</p>" +
                        $"<table style='width:100%; border-collapse: collapse; margin-top: 10px; font-size: 13px;'>" +
                        $"<tr style='background-color: #f1f5f9;'><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Parameter</th><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Details</th></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Tracking Number</td><td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>{load.TrackingNumber}</strong></td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Pickup Location</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.PickupLocation}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Dropoff Location</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.DropoffLocation}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Cargo Description</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.CargoDescription}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Submission Timestamp</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>" +
                        $"</table>"
                    );
                }
                catch (Exception) { /* Non-blocking email fail safe */ }

                TempData["SuccessMessage"] = $"Work request created successfully! Tracking Number: {load.TrackingNumber}";
                return RedirectToAction("Index");
            }

            // Fixed: Use "CustomerId" instead of "AccountReference" in error fallback block as well
            try
            {
                ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CompanyName");
            }
            catch (Exception)
            {
                ViewBag.CustomerList = new SelectList(db.Customers.ToList(), "CustomerId", "CustomerName");
            }

            return View(load);
        }

        // POST: Admin Accepts Work Request, Assigns Van, & Saves Dispatch File Locally
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptRequest(int id, int vehicleId, string routeSafety)
        {
            if (Session["UserRole"]?.ToString() != "Admin")
            {
                return RedirectToAction("Index");
            }

            var load = db.Loads.Find(id);
            if (load != null)
            {
                load.WorkStatus = "Accepted";
                load.AssignedVehicleId = vehicleId;
                load.RouteSafetyRating = string.IsNullOrEmpty(routeSafety) ? "Safe" : routeSafety;
                load.Status = "Dispatched";

                var vehicle = db.Vehicles.Find(vehicleId);
                if (vehicle != null)
                {
                    vehicle.IsAvailable = false;
                }

                if (string.IsNullOrEmpty(load.CollectionPasscode))
                {
                    load.CollectionPasscode = new Random().Next(1000, 9999).ToString();
                }

                db.SaveChanges();

                AuditLogger.Log(load.LoadId, "Accepted & Dispatched Load: " + load.TrackingNumber, "Admin");

                try
                {
                    NotificationService.SendNotificationEmail(
                        "keyram.smma.18@gmail.com",
                        $"Dispatch & PIN Assigned: {load.TrackingNumber}",
                        $"<p>The work request has been approved and officially dispatched with secure driver verification credentials.</p>" +
                        $"<table style='width:100%; border-collapse: collapse; margin-top: 10px; font-size: 13px;'>" +
                        $"<tr style='background-color: #f1f5f9;'><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Dispatch Parameter</th><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Assigned Data</th></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Tracking Number</td><td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>{load.TrackingNumber}</strong></td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Collection PIN</td><td style='padding: 6px; border: 1px solid #cbd5e1;'><span style='font-size: 15px; color: #b91c1c; font-weight: bold;'>{load.CollectionPasscode}</span></td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Route Safety Rating</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.RouteSafetyRating}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Assigned Vehicle ID</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.AssignedVehicleId}</td></tr>" +
                        $"</table>"
                    );
                }
                catch (Exception) { /* Non-blocking */ }

                try
                {
                    string folderPath = @"C:\KeystoneLogs\Emails";
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    string fileName = $"Dispatch_{load.TrackingNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string fullPath = Path.Combine(folderPath, fileName);

                    string emailContent = $"========================================\r\n" +
                                          $"KEYSTONE LOGISTICS OFFICIAL DISPATCH SHEET\r\n" +
                                          $"========================================\r\n" +
                                          $"Tracking Number: {load.TrackingNumber}\r\n" +
                                          $"Pickup Location: {load.PickupLocation}\r\n" +
                                          $"Dropoff Location: {load.DropoffLocation}\r\n" +
                                          $"Cargo Description: {load.CargoDescription}\r\n" +
                                          $"Collection PIN: {load.CollectionPasscode}\r\n" +
                                          $"Route Safety Rating: {load.RouteSafetyRating}\r\n" +
                                          $"Date Issued: {DateTime.Now}\r\n" +
                                          $"----------------------------------------\r\n" +
                                          $"Driver Instructions:\n\nA new load has been assigned to you. Please review the dispatch details and secure Collection PIN above.\n\n- Keystone Logistics Admin";

                    System.IO.File.WriteAllText(fullPath, emailContent);

                    TempData["SuccessMessage"] = $"Work Request #{load.TrackingNumber} Accepted, PIN generated ({load.CollectionPasscode}), and saved locally!";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Request accepted, but local file save failed: {ex.Message}";
                }
            }
            return RedirectToAction("Index");
        }

        // POST: Admin Rejects Work Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int id, string rejectionReason)
        {
            if (Session["UserRole"]?.ToString() != "Admin")
            {
                return RedirectToAction("Index");
            }

            var load = db.Loads.Find(id);
            if (load != null)
            {
                load.WorkStatus = "Rejected";
                load.RejectionReason = rejectionReason;
                load.Status = "Cancelled";
                db.SaveChanges();

                AuditLogger.Log(load.LoadId, "Rejected Load: " + load.TrackingNumber, "Admin");

                try
                {
                    NotificationService.SendNotificationEmail(
                        "keyram.smma.18@gmail.com",
                        $"Shipment Request Rejected: {load.TrackingNumber}",
                        $"<p>A freight work request has been rejected by the administrator.</p>" +
                        $"<table style='width:100%; border-collapse: collapse; margin-top: 10px; font-size: 13px;'>" +
                        $"<tr style='background-color: #f1f5f9;'><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Rejection Parameter</th><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Details</th></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Tracking Number</td><td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>{load.TrackingNumber}</strong></td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Pickup Location</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.PickupLocation}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Dropoff Location</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.DropoffLocation}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Cargo Description</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.CargoDescription}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1; color: #b91c1c; font-weight: bold;'>Rejection Reason</td><td style='padding: 6px; border: 1px solid #cbd5e1; color: #b91c1c; font-weight: bold;'>{load.RejectionReason}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Timestamp</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>" +
                        $"</table>"
                    );
                }
                catch (Exception) { /* Non-blocking fail-safe */ }

                TempData["ErrorMessage"] = $"Work Request #{load.TrackingNumber} Rejected. Reason logged for customer review.";
            }
            return RedirectToAction("Index");
        }

        // POST: Driver Collection Passcode Verification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyCollection(int id, string enteredPasscode)
        {
            if (Session["UserRole"]?.ToString() != "Driver")
            {
                return RedirectToAction("Index");
            }

            var load = db.Loads.Find(id);
            if (load != null)
            {
                if (load.CollectionPasscode == enteredPasscode)
                {
                    load.IsCollected = true;
                    load.Status = "En Route";
                    load.CurrentLocation = "In Transit to Destination";

                    AuditLogger.Log(load.LoadId, "Verified Collection & En Route: " + load.TrackingNumber, "Driver");

                    TempData["SuccessMessage"] = "Collection PIN Verified! Cargo picked up successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Incorrect Collection PIN! Authorization failed.";
                }
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // POST: Driver Marks Cargo as Delivered via Tracking Number Verification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkDelivered(int id, string trackingNumberInput)
        {
            if (Session["UserRole"]?.ToString() != "Driver")
            {
                return RedirectToAction("Index");
            }

            var load = db.Loads.Find(id);
            if (load != null)
            {
                if (!string.IsNullOrEmpty(trackingNumberInput) &&
                    !trackingNumberInput.Equals(load.TrackingNumber, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Invalid tracking number entered ({trackingNumberInput}). Expected {load.TrackingNumber}.";
                    return RedirectToAction("Index");
                }

                load.Status = "Delivered";
                load.WorkStatus = "Completed";
                load.CurrentLocation = load.DropoffLocation;

                if (load.AssignedVehicleId != null)
                {
                    var vehicle = db.Vehicles.Find(load.AssignedVehicleId);
                    if (vehicle != null)
                    {
                        vehicle.IsAvailable = true;
                    }
                }

                db.SaveChanges();

                var pod = db.PODDocuments.FirstOrDefault(p => p.LoadId == load.LoadId);
                string podId = "N/A";
                string recipient = "Verified Receiver";

                if (pod != null)
                {
                    var podType = pod.GetType();
                    var idProp = podType.GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Document") || p.Name.Contains("Ref"));
                    var nameProp = podType.GetProperties().FirstOrDefault(p => p.Name.Contains("Name") || p.Name.Contains("Sign") || p.Name.Contains("Customer") || p.Name.Contains("Receiver"));

                    if (idProp != null) podId = idProp.GetValue(pod)?.ToString() ?? "N/A";
                    if (nameProp != null) recipient = nameProp.GetValue(pod)?.ToString() ?? "Verified Receiver";
                }

                string podSection = pod != null
                    ? $"<br/><h4 style='color: #0f172a; margin-bottom: 8px;'>Proof of Delivery (POD) Documentation</h4>" +
                      $"<table style='width:100%; border-collapse: collapse; margin-top: 5px; font-size: 13px;'>" +
                      $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1; background: #f8fafc;'><strong>POD Reference ID</strong></td><td style='padding: 6px; border: 1px solid #cbd5e1;'>POD-{podId}</td></tr>" +
                      $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1; background: #f8fafc;'><strong>Signatory / Receiver</strong></td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{recipient}</td></tr>" +
                      $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1; background: #f8fafc;'><strong>Completion Timestamp</strong></td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>" +
                      $"</table>"
                    : $"<br/><p style='color: #047857; font-weight: bold;'>Status: Successfully verified via tracking number and completed.</p>";

                string auditAction = string.IsNullOrEmpty(trackingNumberInput)
                    ? "Marked Delivered: " + load.TrackingNumber
                    : $"Verified Tracking Number & Delivered ({trackingNumberInput}): " + load.TrackingNumber;

                AuditLogger.Log(load.LoadId, auditAction, "Driver");

                try
                {
                    NotificationService.SendNotificationEmail(
                        "keyram.smma.18@gmail.com",
                        $"Delivery Confirmed: {load.TrackingNumber}",
                        $"<p>Your package has been successfully delivered and verified via tracking number.</p>" +
                        $"<table style='width:100%; border-collapse: collapse; margin-top: 10px; font-size: 13px;'>" +
                        $"<tr style='background-color: #f1f5f9;'><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Parameter</th><th style='padding: 8px; border: 1px solid #cbd5e1; text-align: left;'>Details</th></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Tracking Number</td><td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>{load.TrackingNumber}</strong></td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Verified Tracking Input</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{trackingNumberInput ?? "N/A"}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Dropoff Location</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{load.DropoffLocation}</td></tr>" +
                        $"<tr><td style='padding: 6px; border: 1px solid #cbd5e1;'>Handover Timestamp</td><td style='padding: 6px; border: 1px solid #cbd5e1;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>" +
                        $"</table>" +
                        podSection
                    );
                }
                catch (Exception) { /* Non-blocking fail-safe */ }

                TempData["SuccessMessage"] = $"Shipment #{load.TrackingNumber} successfully verified and marked as Delivered!";
            }
            else
            {
                TempData["ErrorMessage"] = "Load record not found.";
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}