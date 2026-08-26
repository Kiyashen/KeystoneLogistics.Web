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
    public class CustomersController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: Customers
        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                return View(db.Customers.ToList());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.GetBaseException().Message;
                return View(new List<Customer>());
            }
        }

        // GET: Customers/Loads (Displays freight loads and the review form)
        public ActionResult Loads()
        {
            var loads = db.Loads.Include(l => l.Customer).Include(l => l.Driver).ToList();
            return View(loads);
        }

        // GET: Customers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            Customer customer = db.Customers.Find(id);

            if (customer == null)
            {
                return HttpNotFound();
            }

            return View(customer);
        }

        // GET: Customers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Customer model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                db.Customers.Add(model);
                db.SaveChanges();

                TempData["Success"] = "Customer created successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to create customer. Please try again.";
                return View(model);
            }
        }

        // GET: Customers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            Customer customer = db.Customers.Find(id);

            if (customer == null)
            {
                return HttpNotFound();
            }

            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, Customer model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                db.Entry(model).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Success"] = "Customer updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to update customer. Please try again.";
                return View(model);
            }
        }

        // GET: Customers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            Customer customer = db.Customers.Find(id);

            if (customer == null)
            {
                return HttpNotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Customer customer = db.Customers.Find(id);

                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction("Index");
                }

                // Remove associated loads first to avoid foreign key constraint errors
                var associatedLoads = db.Loads
                    .Where(l => l.CustomerId == id)
                    .ToList();

                if (associatedLoads.Any())
                {
                    db.Loads.RemoveRange(associatedLoads);
                }

                db.Customers.Remove(customer);
                db.SaveChanges();

                TempData["Success"] = "Customer deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to delete customer. Please try again.";
                return RedirectToAction("Index");
            }
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