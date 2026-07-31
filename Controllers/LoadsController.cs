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
    public class LoadsController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: Loads
        public ActionResult Index()
        {
            var loads = db.Loads.Include(l => l.Customer).Include(l => l.Driver);
            return View(loads.ToList());
        }

        // GET: Loads/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Load load = db.Loads.Find(id);
            if (load == null)
            {
                return HttpNotFound();
            }
            return View(load);
        }

        // GET: Loads/Create
        public ActionResult Create()
        {
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "CompanyName");
            ViewBag.DriverId = new SelectList(db.Drivers, "DriverId", "FullName");
            return View();
        }

        // POST: Loads/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "LoadId,TrackingNumber,CustomerId,DriverId,PickupLocation,DropoffLocation,CargoDescription,Status,DispatchedDate,DeliveredDate")] Load load)
        {
            if (ModelState.IsValid)
            {
                db.Loads.Add(load);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "CompanyName", load.CustomerId);
            ViewBag.DriverId = new SelectList(db.Drivers, "DriverId", "FullName", load.DriverId);
            return View(load);
        }

        // GET: Loads/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Load load = db.Loads.Find(id);
            if (load == null)
            {
                return HttpNotFound();
            }
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "CompanyName", load.CustomerId);
            ViewBag.DriverId = new SelectList(db.Drivers, "DriverId", "FullName", load.DriverId);
            return View(load);
        }

        // POST: Loads/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "LoadId,TrackingNumber,CustomerId,DriverId,PickupLocation,DropoffLocation,CargoDescription,Status,DispatchedDate,DeliveredDate")] Load load)
        {
            if (ModelState.IsValid)
            {
                db.Entry(load).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.CustomerId = new SelectList(db.Customers, "CustomerId", "CompanyName", load.CustomerId);
            ViewBag.DriverId = new SelectList(db.Drivers, "DriverId", "FullName", load.DriverId);
            return View(load);
        }

        // GET: Loads/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Load load = db.Loads.Find(id);
            if (load == null)
            {
                return HttpNotFound();
            }
            return View(load);
        }

        // POST: Loads/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Load load = db.Loads.Find(id);
            db.Loads.Remove(load);
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
