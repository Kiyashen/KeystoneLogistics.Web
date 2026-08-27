using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using KeystoneLogistics.Models;
using System.Web;
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

            // Role-based data privacy filtering
            if (userRole == "Customer")
            {
                loads = loads.Where(l => l.CustomerId == userId);
            }
            else if (userRole == "Driver")
            {
                loads = loads.Where(l => l.DriverId == userId || l.WorkStatus == "Accepted");
            }

            ViewBag.AvailableVehicles = db.Vehicles.Where(v => v.IsAvailable == true).ToList();

            return View(loads.ToList());
        }

        // ✅ NEW: GET: Loads/Details/5
        public ActionResult Details(int id)
        {
            // 1. Find the load
            var load = db.Loads.Find(id);
            if (load == null)
            {
                return HttpNotFound();
            }

            // 2. Get all POD documents for this load (simple query – no navigation property needed)
            var podDocuments = db.PODDocuments
                                 .Where(p => p.LoadId == id)
                                 .OrderByDescending(p => p.UploadedAt)
                                 .ToList();

            // 3. Pass them to the view via ViewBag
            ViewBag.PODs = podDocuments;

            // 4. Return the load model to the view
            return View(load);
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
        public ActionResult Create([Bind(Include = "PickupLocation,DropoffLocation,CargoDescription")] Load load)
        {
            if (Session["UserRole"]?.ToString() != "Customer")
            {
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                int count = db.Loads.Count() + 1;
                load.TrackingNumber = $"KL-2026-{count:D3}";

                // Bind to active user session if available, fallback to default customer
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sessionUserId))
                {
                    load.CustomerId = sessionUserId;
                }
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
                db.SaveChanges();

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
                //save the file uploaded using the PODService.
                var podService = new PODService();
                string virtualPath = podService.SavePODFile(podFile);

                if (string.IsNullOrEmpty(virtualPath))
                {
                    TempData["PODError"] = "File upload failed. Please try again.";
                    return RedirectToAction("Details", new { id = LoadId });
                }
                // Create a new proof of delivery record (POD)
                var pod = new PODDocument
                {
                    LoadId = LoadId,
                    FilePath = virtualPath,
                    UploadedAt = DateTime.Now,
                    Notes = notes ?? string.Empty
                };

                db.PODDocuments.Add(pod);
                db.SaveChanges();

                TempData["PODSuccess"] = "Proof of Delivery uploaded successfully!";

            }
            catch (Exception ex)
            {
                //log the exception you can login later.
                TempData["PODError"] = $"An error occurred while uploading the file: {ex.Message}";
            }
            return RedirectToAction("Details", new { id = LoadId });
        }
    }
}