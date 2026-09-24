using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web.Mvc;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class PaymentController : Controller
    {
        private KeystoneLogisticsDBEntities db = new KeystoneLogisticsDBEntities();

        private const decimal BASE_FEE = 500.00m;
        private const decimal RATE_PER_KM = 12.00m;
        private const decimal RATE_PER_KG = 1.50m;
        private const decimal MINIMUM_AMOUNT = 750.00m;
        private const decimal DEFAULT_DISTANCE_KM = 120m;
        private const decimal DEFAULT_WEIGHT_KG = 800m;
        private const decimal DRIVER_PAYOUT_AMOUNT = 1800.00m;

        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential("keyram.smma.18@gmail.com", "mkkpkkmxdleikmjb");
                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress("keyram.smma.18@gmail.com", "Keystone Logistics");
                        mailMessage.To.Add(toEmail);
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;
                        client.Send(mailMessage);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private decimal CalculateDeliveryFee(decimal distanceKm, decimal weightKg)
        {
            decimal total = BASE_FEE + (distanceKm * RATE_PER_KM) + (weightKg * RATE_PER_KG);
            if (total < MINIMUM_AMOUNT) total = MINIMUM_AMOUNT;
            return Math.Round(total, 2);
        }

        private void SendPaymentReceivedEmail(Load load, decimal amount)
        {
            string subject = $"Payment Received - {load.TrackingNumber}";
            string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #0f172a; color: #ffffff; padding: 20px; text-align: center;'>
                    <h2 style='margin: 0; font-size: 20px;'>KEYSTONE LOGISTICS</h2>
                    <p style='margin: 5px 0 0; font-size: 12px; color: #94a3b8;'>Enterprise Freight & Supply Chain Management</p>
                </div>
                <div style='padding: 20px; background-color: #ffffff; color: #334155;'>
                    <h3 style='color: #16a34a; margin-top: 0;'>Payment Successfully Received</h3>
                    <p>A customer has completed payment for the following shipment.</p>
                    <table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
                        <tr style='background-color: #f8fafc;'>
                            <th style='border: 1px solid #cbd5e1; padding: 8px; text-align: left;'>Parameter</th>
                            <th style='border: 1px solid #cbd5e1; padding: 8px; text-align: left;'>Details</th>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Tracking Number</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.TrackingNumber}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Amount Paid</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px; font-size: 16px; color: #16a34a; font-weight: bold;'>R {amount:N2}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Pickup Location</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.PickupLocation}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Dropoff Location</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.DropoffLocation}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Cargo Description</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.CargoDescription}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>New Status</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px; color: #16a34a; font-weight: bold;'>Paid / Processing</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Payment Timestamp</strong></td>
                            <td style='border: 1px solid #cbd5e1; padding: 8px;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td>
                        </tr>
                    </table>
                </div>
            </div>";
            SendEmail("keyram.smma.18@gmail.com", subject, body);
        }

        private void SendTaxInvoiceEmail(Load load, decimal amount)
        {
            var customer = db.Customers.Find(load.CustomerId);
            string customerName = customer != null ? customer.CompanyName : "Customer";
            string customerEmail = customer != null ? customer.Email : "";
            string invoiceNo = "INV-" + load.TrackingNumber;
            decimal vatRate = 0.15m;
            decimal subtotal = Math.Round(amount / (1 + vatRate), 2);
            decimal vat = amount - subtotal;

            string subject = "Tax Invoice " + invoiceNo + " - " + load.TrackingNumber;
            string body = $@"
            <div style='font-family:Arial,sans-serif;max-width:640px;margin:auto;color:#3A3226;'>
                <h2>KEYSTONE LOGISTICS</h2>
                <p>Tax Invoice <strong>{invoiceNo}</strong><br/>Date: {DateTime.Now:yyyy-MM-dd}</p>
                <p><strong>Bill To:</strong> {customerName}<br/>{customerEmail}</p>
                <p>Shipment <strong>{load.TrackingNumber}</strong><br/>
                {load.PickupLocation} → {load.DropoffLocation}<br/>{load.CargoDescription}</p>
                <table style='width:100%;border-collapse:collapse;'>
                    <tr><td style='border:1px solid #ccc;padding:8px;'>Subtotal</td><td style='border:1px solid #ccc;padding:8px;'>R {subtotal:N2}</td></tr>
                    <tr><td style='border:1px solid #ccc;padding:8px;'>VAT 15%</td><td style='border:1px solid #ccc;padding:8px;'>R {vat:N2}</td></tr>
                    <tr><td style='border:1px solid #ccc;padding:8px;'><strong>Total</strong></td><td style='border:1px solid #ccc;padding:8px;'><strong>R {amount:N2}</strong></td></tr>
                </table>
                <p>Paid via PayFast. Open /Payment/Invoice/{load.LoadId} to Print / Save as PDF.</p>
            </div>";

            SendEmail("keyram.smma.18@gmail.com", subject, body);
            if (!string.IsNullOrWhiteSpace(customerEmail))
            {
                SendEmail(customerEmail, subject, body);
            }
        }

        private void SendDriverPayoutEmail(Load load, decimal amount)
        {
            string subject = $"Driver Payout Received - {load.TrackingNumber}";
            string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto;'>
                <h3>Driver Payout Successfully Received</h3>
                <p>Tracking: {load.TrackingNumber}<br/>Amount: R {amount:N2}<br/>Status: Completed</p>
            </div>";
            SendEmail("keyram.smma.18@gmail.com", subject, body);
        }

        public ActionResult Checkout(int? id)
        {
            if (id == null) return RedirectToAction("Index", "Loads");
            if (Session["UserRole"]?.ToString() != "Customer") return RedirectToAction("Index", "Loads");
            var load = db.Loads.Find(id.Value);
            if (load == null) return HttpNotFound();

            decimal distance = DEFAULT_DISTANCE_KM;
            decimal weight = DEFAULT_WEIGHT_KG;
            decimal amount = CalculateDeliveryFee(distance, weight);

            ViewBag.Load = load;
            ViewBag.Amount = amount;
            ViewBag.ItemName = $"Keystone Logistics - {load.TrackingNumber}";
            ViewBag.BaseFee = BASE_FEE;
            ViewBag.DistanceKm = distance;
            ViewBag.WeightKg = weight;
            ViewBag.DistanceCharge = distance * RATE_PER_KM;
            ViewBag.WeightCharge = weight * RATE_PER_KG;
            ViewBag.PayFastUrl = "https://sandbox.payfast.co.za/eng/process";
            ViewBag.MerchantId = "10000100";
            ViewBag.MerchantKey = "46f0cd694581a";
            ViewBag.ReturnUrl = Url.Action("Success", "Payment", new { id = load.LoadId }, Request.Url.Scheme);
            ViewBag.CancelUrl = Url.Action("Cancel", "Payment", new { id = load.LoadId }, Request.Url.Scheme);
            ViewBag.NotifyUrl = Url.Action("Notify", "Payment", null, Request.Url.Scheme);
            return View(load);
        }

        public ActionResult DriverCheckout(int? id)
        {
            if (id == null) return RedirectToAction("Index", "Loads");
            if (Session["UserRole"]?.ToString() != "Admin") return RedirectToAction("Index", "Loads");
            var load = db.Loads.Find(id.Value);
            if (load == null) return HttpNotFound();
            if (load.Status != "Paid / Processing")
            {
                TempData["ErrorMessage"] = "This shipment is not ready for driver payout.";
                return RedirectToAction("Index", "Loads");
            }

            ViewBag.Load = load;
            ViewBag.Amount = DRIVER_PAYOUT_AMOUNT;
            ViewBag.ItemName = $"Driver Payout - {load.TrackingNumber}";
            ViewBag.PayFastUrl = "https://sandbox.payfast.co.za/eng/process";
            ViewBag.MerchantId = "10000100";
            ViewBag.MerchantKey = "46f0cd694581a";
            ViewBag.ReturnUrl = Url.Action("DriverSuccess", "Payment", new { id = load.LoadId }, Request.Url.Scheme);
            ViewBag.CancelUrl = Url.Action("Cancel", "Payment", new { id = load.LoadId }, Request.Url.Scheme);
            ViewBag.NotifyUrl = Url.Action("Notify", "Payment", null, Request.Url.Scheme);
            return View("DriverCheckout", load);
        }

        public ActionResult Invoice(int id)
        {
            var load = db.Loads.Find(id);
            if (load == null) return HttpNotFound();

            var customer = db.Customers.Find(load.CustomerId);
            decimal amount = CalculateDeliveryFee(DEFAULT_DISTANCE_KM, DEFAULT_WEIGHT_KG);

            ViewBag.Amount = amount;
            ViewBag.InvoiceNumber = "INV-" + load.TrackingNumber;
            ViewBag.CustomerName = customer != null ? customer.CompanyName : "Customer";
            ViewBag.CustomerEmail = customer != null ? customer.Email : "";
            return View(load);
        }

        public ActionResult Success(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null)
            {
                decimal amount = CalculateDeliveryFee(DEFAULT_DISTANCE_KM, DEFAULT_WEIGHT_KG);
                load.Status = "Paid / Processing";
                db.SaveChanges();
                SendPaymentReceivedEmail(load, amount);
                SendTaxInvoiceEmail(load, amount);
                TempData["SuccessMessage"] = $"Payment for shipment #{load.TrackingNumber} was successful. Tax invoice emailed.";
            }
            return RedirectToAction("Invoice", new { id = id });
        }

        public ActionResult DriverSuccess(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null)
            {
                load.Status = "Completed";
                load.WorkStatus = "Completed";
                db.SaveChanges();
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = id,
                    Action = "Admin paid the Driver via PayFast - Job Completed",
                    PerformedBy = "Admin",
                    Timestamp = DateTime.Now
                });
                db.SaveChanges();
                SendDriverPayoutEmail(load, DRIVER_PAYOUT_AMOUNT);
                TempData["SuccessMessage"] = $"Driver has been paid for shipment #{load.TrackingNumber}. Status updated to Completed.";
            }
            return RedirectToAction("Index", "Loads");
        }

        public ActionResult Cancel(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null)
            {
                TempData["ErrorMessage"] = $"Payment for shipment #{load.TrackingNumber} was cancelled.";
            }
            return RedirectToAction("Index", "Loads");
        }

        [HttpPost]
        public ActionResult Notify()
        {
            var intnData = Request.Form;
            if (intnData != null && intnData.Count > 0)
            {
                if (int.TryParse(intnData["m_payment_id"], out int loadId))
                {
                    var load = db.Loads.Find(loadId);
                    if (load != null && intnData["payment_status"] == "COMPLETE")
                    {
                        if (load.Status == "Delivered")
                        {
                            decimal amount = CalculateDeliveryFee(DEFAULT_DISTANCE_KM, DEFAULT_WEIGHT_KG);
                            load.Status = "Paid / Processing";
                            db.SaveChanges();
                            SendPaymentReceivedEmail(load, amount);
                            SendTaxInvoiceEmail(load, amount);
                        }
                        else if (load.Status == "Paid / Processing")
                        {
                            load.Status = "Completed";
                            load.WorkStatus = "Completed";
                            db.SaveChanges();
                            SendDriverPayoutEmail(load, DRIVER_PAYOUT_AMOUNT);
                        }
                    }
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}