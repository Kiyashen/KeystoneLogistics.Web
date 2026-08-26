using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class DriversController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: Drivers
        public ActionResult Index()
        {
            var drivers = db.Drivers.Include(d => d.Loads).ToList();
            return View(drivers);
        }

        // GET: Drivers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }
            Driver driver = db.Drivers.Find(id);
            if (driver == null)
            {
                return HttpNotFound();
            }
            return View(driver);
        }

        // GET: Drivers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Drivers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "DriverId,FullName,Phone,VehicleRegistration,IsAvailable")] Driver driver)
        {
            if (ModelState.IsValid)
            {
                db.Drivers.Add(driver);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(driver);
        }

        // GET: Drivers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }
            Driver driver = db.Drivers.Find(id);
            if (driver == null)
            {
                return HttpNotFound();
            }
            return View(driver);
        }

        // POST: Drivers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "DriverId,FullName,Phone,VehicleRegistration,IsAvailable")] Driver driver)
        {
            if (ModelState.IsValid)
            {
                db.Entry(driver).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(driver);
        }

        // POST: Drivers/ToggleAvailability/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleAvailability(int id)
        {
            Driver driver = db.Drivers.Find(id);
            if (driver == null)
            {
                return HttpNotFound();
            }

            // Null is treated as "not confirmed available" and toggles to Available,
            // matching the Index view's existing badge logic where null and false
            // both render as "Busy".
            driver.IsAvailable = !(driver.IsAvailable ?? false);
            db.Entry(driver).State = EntityState.Modified;
            db.SaveChanges();

            TempData["SuccessMessage"] = driver.FullName + " is now marked as "
                + (driver.IsAvailable.Value ? "Available" : "Busy") + ".";

            return RedirectToAction("Index");
        }

        // GET: Drivers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }
            Driver driver = db.Drivers.Find(id);
            if (driver == null)
            {
                return HttpNotFound();
            }
            return View(driver);
        }

        // POST: Drivers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Driver driver = db.Drivers.Find(id);
            if (driver == null)
            {
                return RedirectToAction("Index");
            }

            // Remove associated loads first to prevent foreign key constraint conflicts
            var associatedLoads = db.Loads.Where(l => l.DriverId == id).ToList();
            if (associatedLoads.Any())
            {
                db.Loads.RemoveRange(associatedLoads);
            }

            db.Drivers.Remove(driver);
            db.SaveChanges();
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