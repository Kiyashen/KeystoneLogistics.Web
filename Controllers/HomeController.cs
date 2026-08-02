using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class HomeController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

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
                                .ToList()
            };

            return View(viewModel);
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