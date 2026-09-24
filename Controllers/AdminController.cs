using System;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class AdminController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        // POST: Admin/PayDriver/5
        [HttpPost]
        public ActionResult PayDriver(int id)
        {
            if (Session["UserRole"]?.ToString() != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            var load = db.Loads.Find(id);
            if (load == null)
            {
                return HttpNotFound();
            }

            // Safely set IsDriverPaid via reflection if the property exists in your model
            var loadType = load.GetType();
            var isDriverPaidProp = loadType.GetProperty("IsDriverPaid");
            if (isDriverPaidProp != null && isDriverPaidProp.CanWrite)
            {
                isDriverPaidProp.SetValue(load, true);
            }

            load.Status = "Completed & Driver Paid";
            db.SaveChanges();

            // Send notification email to the assigned driver safely
            SendDriverNotificationEmail(load);

            TempData["SuccessMessage"] = $"Driver payout processed successfully for shipment #{load.TrackingNumber}!";
            return RedirectToAction("ManageLoads", "Admin");
        }

        private void SendDriverNotificationEmail(Load load)
        {
            try
            {
                string driverEmail = "driver@keystonelogistics.co.za";
                var loadType = load.GetType();
                var driverProp = loadType.GetProperty("Driver");
                if (driverProp != null)
                {
                    var driverObj = driverProp.GetValue(load);
                    if (driverObj != null)
                    {
                        var emailProp = driverObj.GetType().GetProperty("Email") ?? driverObj.GetType().GetProperty("DriverEmail");
                        if (emailProp != null)
                        {
                            var val = emailProp.GetValue(driverObj);
                            if (val != null) driverEmail = val.ToString();
                        }
                    }
                }

                var fromAddress = new MailAddress("keyram.smma.18@gmail.com", "Keystone Logistics Finance");
                var toAddress = new MailAddress(driverEmail, "Driver");
                string subject = $"Payout Processed - Shipment #{load.TrackingNumber}";
                string body = $"Dear Driver,\n\n" +
                              $"Your payout for completing shipment #{load.TrackingNumber} has been processed successfully.\n" +
                              $"Funds have been transferred to your registered account.\n\n" +
                              $"Thank you for your hard work!";

                using (var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    Credentials = new NetworkCredential("keyram.smma.18@gmail.com", "mkkpkkmxdleikmjb"),
                    EnableSsl = true
                })
                {
                    using (var message = new MailMessage(fromAddress, toAddress) { Subject = subject, Body = body })
                    {
                        smtp.Send(message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Driver email error: {ex.Message}");
            }
        }
    }
}