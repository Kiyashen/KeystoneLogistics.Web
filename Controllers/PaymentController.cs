using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
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
        private const decimal DEFAULT_WEIGHT_KG = 800m;
        private const decimal ROAD_FACTOR = 1.35m;
        private const decimal DRIVER_FIXED_PAY = 1800.00m;
        private const string AreaError = "We only accommodate deliveries between Durban and Pietermaritzburg and the surrounding areas.";
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
        private decimal RecordedWeight(Load load)
        {
            var match = Regex.Match(load?.CargoDescription ?? "", @"WT:(\d+(?:\.\d+)?)");
            if (match.Success &&
                decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal kg) &&
                kg > 0)
                return kg;
            return DEFAULT_WEIGHT_KG;
        }
        private decimal CalculateDeliveryFee(decimal distanceKm, decimal weightKg)
        {
            decimal total = BASE_FEE + (distanceKm * RATE_PER_KM) + (weightKg * RATE_PER_KG);
            if (total < MINIMUM_AMOUNT) total = MINIMUM_AMOUNT;
            return Math.Round(total, 2);
        }
        private decimal CalculateDriverPayout()
        {
            return DRIVER_FIXED_PAY;
        }
        private bool TryResolve(Load load, out decimal distanceKm, out string error)
        {
            distanceKm = 0;
            error = null;
            if (load == null || string.IsNullOrWhiteSpace(load.PickupLocation) || string.IsNullOrWhiteSpace(load.DropoffLocation))
            {
                error = "Pickup and dropoff are required.";
                return false;
            }
            if (!TryLocate(load.PickupLocation, out double pLat, out double pLng))
            {
                error = "Could not find that pickup on the map. Type the address again.";
                return false;
            }
            if (!TryLocate(load.DropoffLocation, out double dLat, out double dLng))
            {
                error = "Could not find that dropoff on the map. Type the address again.";
                return false;
            }
            if (!InServiceArea(pLat, pLng) || !InServiceArea(dLat, dLng))
            {
                error = AreaError;
                return false;
            }
            double straight = HaversineKm(pLat, pLng, dLat, dLng);
            distanceKm = Math.Round((decimal)straight * ROAD_FACTOR, 1);
            if (distanceKm < 1m) distanceKm = 1m;
            return true;
        }
        private bool InServiceArea(double lat, double lng)
        {
            return lat <= -29.35 && lat >= -30.20 && lng >= 29.95 && lng <= 31.25;
        }
        private bool TryLocate(string text, out double lat, out double lng)
        {
            lat = 0;
            lng = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var coord = Regex.Match(text, @"(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)");
            if (coord.Success &&
                double.TryParse(coord.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
                double.TryParse(coord.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lng))
            {
                return true;
            }
            string cleaned = Regex.Replace(text, @"\b(hub|terminal|depot|warehouse|centre|center|yard|port|distribution)\b", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            var queries = new List<string> { text + ", South Africa", text };
            if (!string.IsNullOrWhiteSpace(cleaned)) queries.Add(cleaned + ", South Africa");
            string lower = text.ToLowerInvariant();
            string[] towns = { "pietermaritzburg", "pmb", "durban", "pinetown", "umhlanga", "chatsworth", "westville", "hillcrest", "ballito", "howick", "amanzimtoti", "phoenix", "verulam" };
            foreach (string town in towns)
            {
                if (lower.Contains(town))
                    queries.Add((town == "pmb" ? "Pietermaritzburg" : town) + ", South Africa");
            }
            foreach (string query in queries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    string url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=za&q=" +
                                 Uri.EscapeDataString(query);
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "KeystoneLogistics/1.0 (student project)");
                        string json = client.DownloadString(url);
                        var latMatch = Regex.Match(json, "\"lat\"\\s*:\\s*\"(-?\\d+\\.\\d+)\"");
                        var lngMatch = Regex.Match(json, "\"lon\"\\s*:\\s*\"(-?\\d+\\.\\d+)\"");
                        if (!latMatch.Success || !lngMatch.Success) continue;
                        lat = double.Parse(latMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        lng = double.Parse(lngMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
        private double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0;
            double dLat = ToRad(lat2 - lat1);
            double dLng = ToRad(lng2 - lng1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * (2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
        }
        private double ToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }
        private void SendPaymentReceivedEmail(Load load, decimal amount, decimal distanceKm)
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
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Tracking Number</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.TrackingNumber}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Distance</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{distanceKm:N1} km</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Amount Paid</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px; color: #16a34a; font-weight: bold;'>R {amount:N2}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Pickup</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.PickupLocation}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Dropoff</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.DropoffLocation}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Cargo</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.CargoDescription}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Status</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>Paid / Processing</td></tr>
                    </table>
                </div>
            </div>";
            SendEmail("keyram.smma.18@gmail.com", subject, body);
        }
        private void SendTaxInvoiceEmail(Load load, decimal amount, decimal distanceKm)
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
                {load.PickupLocation} to {load.DropoffLocation}<br/>
                Distance: {distanceKm:N1} km<br/>{load.CargoDescription}</p>
                <table style='width:100%;border-collapse:collapse;'>
                    <tr><td style='border:1px solid #ccc;padding:8px;'>Subtotal</td><td style='border:1px solid #ccc;padding:8px;'>R {subtotal:N2}</td></tr>
                    <tr><td style='border:1px solid #ccc;padding:8px;'>VAT 15%</td><td style='border:1px solid #ccc;padding:8px;'>R {vat:N2}</td></tr>
                    <tr><td style='border:1px solid #ccc;padding:8px;'><strong>Total</strong></td><td style='border:1px solid #ccc;padding:8px;'><strong>R {amount:N2}</strong></td></tr>
                </table>
            </div>";
            SendEmail("keyram.smma.18@gmail.com", subject, body);
            if (!string.IsNullOrWhiteSpace(customerEmail)) SendEmail(customerEmail, subject, body);
        }
        private void SendDriverPayoutEmail(Load load, decimal amount)
        {
            string driverName = "Driver";
            if (load.DriverId.HasValue)
            {
                var driver = db.Drivers.Find(load.DriverId.Value);
                if (driver != null && !string.IsNullOrWhiteSpace(driver.FullName))
                    driverName = driver.FullName;
            }
            string subject = $"Driver Payout Received - {load.TrackingNumber}";
            string body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #0f172a; color: #ffffff; padding: 20px; text-align: center;'>
                    <h2 style='margin: 0; font-size: 20px;'>KEYSTONE LOGISTICS</h2>
                    <p style='margin: 5px 0 0; font-size: 12px; color: #94a3b8;'>Driver payout confirmation</p>
                </div>
                <div style='padding: 20px; background-color: #ffffff; color: #334155;'>
                    <h3 style='color: #2F6F5E; margin-top: 0;'>Fixed payout paid to {driverName}</h3>
                    <p>Every driver is paid the same company rate for a completed job. Distance is not used.</p>
                    <table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Tracking Number</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.TrackingNumber}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Driver</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{driverName}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Route</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>{load.PickupLocation} to {load.DropoffLocation}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Pay type</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>Fixed rate, same for every driver</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Total paid</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px; color: #2F6F5E; font-weight: bold;'>R {amount:N2}</td></tr>
                        <tr><td style='border: 1px solid #cbd5e1; padding: 8px;'><strong>Status</strong></td><td style='border: 1px solid #cbd5e1; padding: 8px;'>Completed</td></tr>
                    </table>
                </div>
            </div>";
            SendEmail("keyram.smma.18@gmail.com", subject, body);
        }
        public ActionResult Checkout(int? id)
        {
            if (id == null) return RedirectToAction("Index", "Loads");
            if (Session["UserRole"]?.ToString() != "Customer") return RedirectToAction("Index", "Loads");
            var load = db.Loads.Find(id.Value);
            if (load == null) return HttpNotFound();
            if (!TryResolve(load, out decimal distance, out string error))
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index", "Loads");
            }
            decimal weight = RecordedWeight(load);
            decimal amount = CalculateDeliveryFee(distance, weight);
            ViewBag.Load = load;
            ViewBag.Amount = amount;
            ViewBag.ItemName = $"Keystone Logistics - {load.TrackingNumber}";
            ViewBag.BaseFee = BASE_FEE;
            ViewBag.DistanceKm = distance;
            ViewBag.WeightKg = weight;
            ViewBag.DistanceCharge = Math.Round(distance * RATE_PER_KM, 2);
            ViewBag.WeightCharge = Math.Round(weight * RATE_PER_KG, 2);
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
            decimal payout = CalculateDriverPayout();
            ViewBag.Load = load;
            ViewBag.Amount = payout;
            ViewBag.DriverBase = payout;
            ViewBag.DriverRate = 0m;
            ViewBag.DistanceKm = 0m;
            ViewBag.DistanceCharge = 0m;
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
            TryResolve(load, out decimal distance, out _);
            if (distance <= 0) distance = 1;
            ViewBag.Amount = CalculateDeliveryFee(distance, RecordedWeight(load));
            ViewBag.DistanceKm = distance;
            ViewBag.InvoiceNumber = "INV-" + load.TrackingNumber;
            ViewBag.CustomerName = customer != null ? customer.CompanyName : "Customer";
            ViewBag.CustomerEmail = customer != null ? customer.Email : "";
            return View(load);
        }
        public ActionResult Success(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null && TryResolve(load, out decimal distance, out _))
            {
                decimal amount = CalculateDeliveryFee(distance, RecordedWeight(load));
                load.Status = "Paid / Processing";
                db.SaveChanges();
                SendPaymentReceivedEmail(load, amount, distance);
                SendTaxInvoiceEmail(load, amount, distance);
                TempData["SuccessMessage"] = $"Payment for shipment #{load.TrackingNumber} was successful. Tax invoice emailed.";
            }
            return RedirectToAction("Invoice", new { id = id });
        }
        public ActionResult DriverSuccess(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null)
            {
                decimal payout = CalculateDriverPayout();
                load.Status = "Completed";
                load.WorkStatus = "Completed";
                db.SaveChanges();
                db.AuditLogs.Add(new AuditLog
                {
                    LoadId = id,
                    Action = "Admin paid the driver the fixed rate of R1800",
                    PerformedBy = "Admin",
                    Timestamp = DateTime.Now
                });
                db.SaveChanges();
                SendDriverPayoutEmail(load, payout);
                TempData["SuccessMessage"] = $"Driver has been paid the fixed rate of R {payout:N2} for shipment #{load.TrackingNumber}.";
            }
            return RedirectToAction("Index", "Loads");
        }
        public ActionResult Cancel(int id)
        {
            var load = db.Loads.Find(id);
            if (load != null)
                TempData["ErrorMessage"] = $"Payment for shipment #{load.TrackingNumber} was cancelled.";
            return RedirectToAction("Index", "Loads");
        }
        [HttpPost]
        public ActionResult Notify()
        {
            var intnData = Request.Form;
            if (intnData != null && intnData.Count > 0 && int.TryParse(intnData["m_payment_id"], out int loadId))
            {
                var load = db.Loads.Find(loadId);
                if (load != null && intnData["payment_status"] == "COMPLETE")
                {
                    if (load.Status == "Delivered" && TryResolve(load, out decimal distance, out _))
                    {
                        decimal amount = CalculateDeliveryFee(distance, RecordedWeight(load));
                        load.Status = "Paid / Processing";
                        db.SaveChanges();
                        SendPaymentReceivedEmail(load, amount, distance);
                        SendTaxInvoiceEmail(load, amount, distance);
                    }
                    else if (load.Status == "Paid / Processing")
                    {
                        decimal payout = CalculateDriverPayout();
                        load.Status = "Completed";
                        load.WorkStatus = "Completed";
                        db.SaveChanges();
                        SendDriverPayoutEmail(load, payout);
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