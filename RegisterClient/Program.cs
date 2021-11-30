using System;
using Microsoft.ConfigurationManagement.Messaging.Framework;
using Microsoft.ConfigurationManagement.Messaging.Messages;
using Microsoft.ConfigurationManagement.Messaging.Sender.Http;

namespace RegisterClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // HTTP sender is used for sending messages to the MP
            HttpSender sender = new HttpSender();
            // Generate certificate for signing messages
            MessageCertificateX509File certificate = new MessageCertificateX509File("CD8F5BCC42D83EF62A4F367612E9501B9F9BB6D5");
            // Create a registration request
            ConfigMgrRegistrationRequest registrationRequest = new ConfigMgrRegistrationRequest();
            // Add our certificate for message signing
            registrationRequest.AddCertificateToMessage(certificate, CertificatePurposes.Signing);
            // Set the destination hostname
            registrationRequest.Settings.HostName = "atlas.aperture.science";
            // Discover local properties for registration metadata
            Console.WriteLine("[+] Discovering local properties");
            registrationRequest.Discover();
            Console.WriteLine($"ClientFqdn: {registrationRequest.ClientFqdn}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName + <domain name>
            Console.WriteLine($"NetBiosName: {registrationRequest.NetBiosName}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName
            Console.WriteLine($"RequestedSmsId: {registrationRequest.RequestedSmsId}");
            registrationRequest.SiteCode = "PS1";
            Console.WriteLine($"SiteCode: {registrationRequest.SiteCode}");
            Console.WriteLine($"SmsId: {registrationRequest.SmsId}");
            Console.WriteLine($"[+] Sending HTTP registration request to {registrationRequest.Settings.HostName}:{registrationRequest.Settings.HttpPort}");
            // Register client and wait for a confirmation with the SMSID
            SmsClientId clientId = registrationRequest.RegisterClient(sender, TimeSpan.FromMinutes(5));
            // Send data to the site
            registrationRequest.Validate(sender);
            //registrationRequest.SendMessage(sender);
        }
    }
}
