using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using KeystoneLogistics.Models;
using KeystoneLogistics.Services;
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
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Loads");
                }
                else if (user.Role == "Driver")
                {
                    return RedirectToAction("Index", "Loads");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
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
            string tempPassword = "Temp" + new Random().Next(1000, 9999);
            var user = db.Users.FirstOrDefault(u => u.Email == email || u.Username == email);
            if (user != null)
            {
                user.Password = tempPassword;
                db.SaveChanges();
            }
            string recipientEmail = user != null && !string.IsNullOrEmpty(user.Email) ? user.Email : email;
            try
            {
                NotificationService.SendTemporaryPassword(recipientEmail, tempPassword);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Email sending failed: " + ex.Message);
            }
            TempData["SuccessMessage"] = "If your email is registered, a temporary password has been sent to your inbox.";
            return RedirectToAction("Login");
        }
    }
}