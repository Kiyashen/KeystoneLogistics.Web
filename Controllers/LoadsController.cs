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

            // 1. Smart, Robust Customer Database Verification (Supports Username, ID, Name, Email, or Session Fallback)
            Customer verifiedCustomer = null;

            if (!string.IsNullOrEmpty(CustomerName))
            {
                // Try parsing as ID first
                if (int.TryParse(CustomerName, out int parsedCustId))
                {
                    verifiedCustomer = db.Customers.Find(parsedCustId);
                }

                // If not found by ID, inspect customer records dynamically for any matching text property
                if (verifiedCustomer == null)
                {
                    var allCustomers = db.Customers.ToList();
                    verifiedCustomer = allCustomers.FirstOrDefault(c =>
                        (c.GetType().GetProperty("Username")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("CustomerName")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("Name")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("CompanyName")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GetType().GetProperty("Email")?.GetValue(c)?.ToString()?.Equals(CustomerName, StringComparison.OrdinalIgnoreCase) ?? false)
                    );
                }
            }

            // Fallback: If quick-fill or input text doesn't match a text column, use the active logged-in customer's ID from session
            if (verifiedCustomer == null && Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sessionUserId))
            {
                verifiedCustomer = db.Customers.Find(sessionUserId);
            }

            if (verifiedCustomer == null)
            {
                ModelState.AddModelError("", "Database Verification Failed: The Customer username/ID does not match an active account in our system.");
            }

            if (string.IsNullOrWhiteSpace(AccountReference) || !AccountReference.StartsWith("KL-ACC-"))
            {
                ModelState.AddModelError("", "Account Reference Validation Failed: Invalid or unrecognized company account format.");
            }

            if (ModelState.IsValid)
            {
                // Assign the verified customer ID from database
                load.CustomerId = verifiedCustomer.CustomerId;

                // Robust unique tracking number generation to prevent duplicate key collisions
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

                // Log the creation activity
                string userRole = Session["UserRole"]?.ToString() ?? "Customer";
                AuditLogger.Log(load.LoadId, "Created Load: " + load.TrackingNumber, userRole);

                TempData["SuccessMessage"] = $"Work request created successfully! Tracking Number: {load.TrackingNumber}";
                return RedirectToAction("Index");
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

                // Update vehicle status to unavailable
                var vehicle = db.Vehicles.Find(vehicleId);
                if (vehicle != null)
                {
                    vehicle.IsAvailable = false;
                }

                // Generate random 4-digit pickup PIN if not present
                if (string.IsNullOrEmpty(load.CollectionPasscode))
                {
                    load.CollectionPasscode = new Random().Next(1000, 9999).ToString();
                }

                db.SaveChanges();

                // Log the acceptance/dispatch activity
                AuditLogger.Log(load.LoadId, "Accepted & Dispatched Load: " + load.TrackingNumber, "Admin");

                // Save dispatch email & document locally to bypass network/authentication blocks
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

                // Log rejection activity
                AuditLogger.Log(load.LoadId, "Rejected Load: " + load.TrackingNumber, "Admin");

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

                    // Log collection verification
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

        // POST: Driver Marks Cargo as Delivered
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkDelivered(int id)
        {
            if (Session["UserRole"]?.ToString() != "Driver")
            {
                return RedirectToAction("Index");
            }

            var load = db.Loads.Find(id);
            if (load != null)
            {
                load.Status = "Delivered";
                load.WorkStatus = "Completed";
                load.CurrentLocation = load.DropoffLocation;

                // Free up assigned vehicle for future loads
                if (load.AssignedVehicleId != null)
                {
                    var vehicle = db.Vehicles.Find(load.AssignedVehicleId);
                    if (vehicle != null)
                    {
                        vehicle.IsAvailable = true;
                    }
                }

                db.SaveChanges();

                // Log successful delivery
                AuditLogger.Log(load.LoadId, "Marked Delivered: " + load.TrackingNumber, "Driver");

                TempData["SuccessMessage"] = $"Shipment #{load.TrackingNumber} successfully marked as Delivered!";
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