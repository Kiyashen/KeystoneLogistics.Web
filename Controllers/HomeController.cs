using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class HomeController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // Static in-memory lists to handle reviews and admin alerts smoothly without database errors
        public static List<ReviewModel> StaticReviews = new List<ReviewModel>
        {
            new ReviewModel { Id = 1, CustomerName = "Sipho Mthethwa", Rating = 5, Comment = "Incredible service! Our shipment arrived ahead of schedule.", DatePosted = DateTime.Now.AddDays(-2), IsBadReview = false, AdminNotified = true },
            new ReviewModel { Id = 2, CustomerName = "Jessica Naidoo", Rating = 5, Comment = "The secure vault containment is top tier.", DatePosted = DateTime.Now.AddDays(-1), IsBadReview = false, AdminNotified = true }
        };

        // GET: Home (Executive Dashboard)
        public ActionResult Index()
        {
            var viewModel = new DashboardViewModel
            {
                TotalActiveLoads = db.Loads.Count(l => l.Status != "Delivered"),
                PendingDeliveryLoads = db.Loads.Count(l => l.Status == "Dispatched" || l.Status == "En Route"),
                AvailableDriversCount = db.Drivers.Count(d => d.IsAvailable == true),
                TotalCustomersCount = db.Customers.Count(),
                RecentLoads = db.Loads
                                    .Include(l => l.Customer)
                                    .Include(l => l.Driver)
                                    .OrderByDescending(l => l.LoadId)
                                    .Take(5)
                                    .ToList(),

                // Pass reviews to the home page
                Reviews = StaticReviews.OrderByDescending(r => r.DatePosted).Take(6).ToList()
            };

            return View(viewModel);
        }

        // POST: Home/SubmitReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitReview(int rating, string comment)
        {
            try
            {
                bool isBad = rating <= 2;

                var review = new ReviewModel
                {
                    Id = StaticReviews.Count + 1,
                    CustomerName = User.Identity.IsAuthenticated ? User.Identity.Name : "Valued Customer",
                    Rating = rating,
                    Comment = comment,
                    DatePosted = DateTime.Now,
                    IsBadReview = isBad,
                    AdminNotified = false // Triggers the admin popup notification alert
                };

                // Add to our review list so it appears instantly
                StaticReviews.Insert(0, review);

                TempData["SuccessMessage"] = "Thank you! Your review has been submitted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while saving your review. Please try again.";
            }

            // Redirect back to the Customer Index page so the user stays in the customer section
            return RedirectToAction("Index", "Customer");
        }

        // GET: Home/About
        public ActionResult About()
        {
            ViewBag.Message = "Keystone Logistics Fleet & Freight Management System Overview.";
            return View();
        }

        // GET: Home/Contact
        public ActionResult Contact()
        {
            ViewBag.Message = "Operations Control & Technical Support Center.";
            return View();
        }

        // POST: Home/Contact (Handles support inquiries and forwards them via email to your phone)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(string name, string email, string message)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(message))
            {
                try
                {
                    using (var smtpClient = new SmtpClient())
                    {
                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress("keyram.smma.18@gmail.com", "Keystone Logistics Support"),
                            Subject = $"New Dispatch Inquiry: {name}",
                            Body = $"You have received a new support inquiry from the portal:\n\nName: {name}\nEmail: {email}\n\nMessage:\n{message}",
                            IsBodyHtml = false
                        };

                        mailMessage.To.Add("keyram.smma.18@gmail.com");

                        smtpClient.Send(mailMessage);
                    }

                    TempData["SuccessMessage"] = "Your support inquiry has been successfully sent to your phone via email!";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to send inquiry email: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please fill in all fields before submitting.";
            }

            return RedirectToAction("Contact");
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