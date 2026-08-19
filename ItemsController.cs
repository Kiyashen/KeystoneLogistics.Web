using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class ItemsController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // POST: Items/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "LoadId,TrackingNumber,CustomerId,DriverId,PickupLocation,DropoffLocation,CargoDescription,Status,DispatchedDate,DeliveredDate")] Load load)
        {
            if (ModelState.IsValid)
            {
                db.Loads.Add(load);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Load {load.TrackingNumber} was successfully created!";
                return RedirectToAction("Index");
            }

            return View(load);
        }

        // POST: Items/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "LoadId,TrackingNumber,CustomerId,DriverId,PickupLocation,DropoffLocation,CargoDescription,Status,DispatchedDate,DeliveredDate")] Load load)
        {
            if (ModelState.IsValid)
            {
                db.Entry(load).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Load {load.TrackingNumber} was successfully updated!";
                return RedirectToAction("Index");
            }

            return View(load);
        }

        // POST: Items/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Load load = db.Loads.Find(id);
            if (load == null)
            {
                TempData["ErrorMessage"] = "Load not found.";
                return RedirectToAction("Index");
            }

            string trackingNumber = load.TrackingNumber;
            db.Loads.Remove(load);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Load {trackingNumber} was successfully deleted!";
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