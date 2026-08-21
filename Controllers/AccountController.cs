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
    }
}