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
        public ActionResult Index()
        {
            try
            {
                return View(db.Customers.ToList());
            }
            catch (System.Data.Entity.Core.EntityException)
            {
                TempData["ErrorMessage"] = "Database connection error. Please check the connection string and ensure the database server is available.";
                // Return an empty list so the view can render safely
                return View(new List<Customer>());
            }
        }

        // GET: Customers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
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
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "CustomerId,CompanyName,ContactPerson,Email,Phone")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    db.Customers.Add(customer);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Customer created successfully.";
                    return RedirectToAction("Index");
                }
                catch (System.Data.Entity.Core.EntityException)
                {
                    TempData["ErrorMessage"] = "Unable to save customer — database connection error.";
                    return View(customer);
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An unexpected error occurred while creating the customer.";
                    return View(customer);
                }
            }

            return View(customer);
        }

        // GET: Customers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            return View(customer);
        }

        // POST: Customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CustomerId,CompanyName,ContactPerson,Email,Phone")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    db.Entry(customer).State = EntityState.Modified;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Customer updated successfully.";
                    return RedirectToAction("Index");
                }
                catch (System.Data.Entity.Core.EntityException)
                {
                    TempData["ErrorMessage"] = "Unable to update customer — database connection error.";
                    return View(customer);
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An unexpected error occurred while updating the customer.";
                    return View(customer);
                }
            }
            return View(customer);
        }

        // GET: Customers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
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
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction("Index");
                }

                db.Customers.Remove(customer);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Customer deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Core.EntityException)
            {
                TempData["ErrorMessage"] = "Unable to delete customer — database connection error.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the customer.";
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
