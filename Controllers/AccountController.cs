using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class AccountController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            var user = db.Users.FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                Session["UserId"] = user.UserId;
                Session["Username"] = user.Username;
                Session["UserRole"] = user.Role;

                // Role-based redirection logic
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Loads"); // Admin Dashboard / Work Requests
                }
                else if (user.Role == "Driver")
                {
                    return RedirectToAction("Index", "Loads"); // Driver Portal
                }
                else // Customer
                {
                    return RedirectToAction("Index", "Loads"); // Customer Portal
                }
            }

            ViewBag.ErrorMessage = "Invalid Username or Password.";
            return View();
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: Account/ForgotPassword
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            // Generate a random temporary password
            string tempPassword = "Temp" + new Random().Next(1000, 9999);

            // Check if the user exists in the database to update their password
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                user.Password = tempPassword;
                db.SaveChanges();
            }

            // ALWAYS write to the text file in the background so you can see it in your project folder
            try
            {
                string filePath = Server.MapPath("~/PasswordInbox.txt");
                string logEntry = $"-----------------------------------------\n" +
                                  $"Time: {DateTime.Now}\n" +
                                  $"Entered Email: {email}\n" +
                                  $"Temporary Password: {tempPassword}\n" +
                                  $"Database Match: {(user != null ? "Found & Updated" : "Email not in DB, but generated anyway")}\n" +
                                  $"-----------------------------------------\n\n";

                System.IO.File.AppendAllText(filePath, logEntry);
            }
            catch (Exception)
            {
                // Fallback if file write fails
            }

            // Professional, user-friendly message
            TempData["SuccessMessage"] = "If your email is registered, a temporary password has been sent to your inbox.";
            return RedirectToAction("Login");
        }
    }
}