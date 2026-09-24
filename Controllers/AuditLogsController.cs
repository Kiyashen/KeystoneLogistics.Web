using System.Linq;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class AuditLogsController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // GET: AuditLogs
        public ActionResult Index()
        {
            if (Session["UserRole"]?.ToString() != "Admin")
            {
                return RedirectToAction("Index", "Loads");
            }

            // Fetch audit logs sorted by Timestamp descending (newest first)
            var auditLogs = db.AuditLogs.OrderByDescending(a => a.Timestamp).ToList();
            return View(auditLogs);
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