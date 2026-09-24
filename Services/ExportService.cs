using System;
using System.Collections.Generic;
using System.Text;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Services
{
    public class ExportService
    {
        public byte[] ExportLoadsToCsv(List<Load> loads)
        {
            var csv = new StringBuilder();

            // CSV headers
            csv.AppendLine(
                "LoadId,TrackingNumber,CustomerId,DriverId,PickupLocation,DropoffLocation,CargoDescription,Status,DispatchedDate,DeliveredDate"
            );

            foreach (var load in loads)
            {
                csv.AppendLine(
                    $"{load.LoadId}," +
                    $"\"{load.TrackingNumber}\"," +
                    $"{load.CustomerId}," +
                    $"{load.DriverId}," +
                    $"\"{load.PickupLocation}\"," +
                    $"\"{load.DropoffLocation}\"," +
                    $"\"{load.CargoDescription}\"," +
                    $"\"{load.Status}\"," +
                    $"{load.DispatchedDate:yyyy-MM-dd HH:mm:ss}," +
                    $"{load.DeliveredDate:yyyy-MM-dd HH:mm:ss}"
                );
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
    }
}