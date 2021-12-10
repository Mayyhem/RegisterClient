using System;
using System.IO;
using System.Xml;
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
            //MessageCertificateX509File certificate = new MessageCertificateX509File("CD8F5BCC42D83EF62A4F367612E9501B9F9BB6D5");
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
            Console.WriteLine($"ClientFqdn: {registrationRequest.ClientFqdn}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName + <domain name>
            Console.WriteLine($"NetBiosName: {registrationRequest.NetBiosName}"); // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName\ComputerName
            Console.WriteLine($"RequestedSmsId: {registrationRequest.RequestedSmsId}");
            registrationRequest.SiteCode = "PS1";
            Console.WriteLine($"SiteCode: {registrationRequest.SiteCode}");
            Console.WriteLine($"SmsId: {registrationRequest.SmsId}"); // Should be empty so that a new unique ID can be set for the new object
            Console.WriteLine($"[+] Sending HTTP registration request to {registrationRequest.Settings.HostName}:{registrationRequest.Settings.HttpPort}");
            // Register client and wait for a confirmation with the SMSID
            //SmsClientId clientId = registrationRequest.RegisterClient(sender, TimeSpan.FromMinutes(5));
            // Send data to the site
            //registrationRequest.Validate(sender);
            //registrationRequest.SendMessage(sender);
            

            // Build a gratuitous heartbeat DDR to send inventory information for the newly created system
            ConfigMgrDataDiscoveryRecordMessage ddrMessage = new ConfigMgrDataDiscoveryRecordMessage();
            ddrMessage.Discover();
            ddrMessage.InventoryReport = ddrMessage.BuildInventoryMessage(ddrMessage.DdrInstances);
            //Console.WriteLine(ddrMessage.InventoryReport.ReportBody.RawXml);



            // Read XML from discovered DDR data
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
            xmlReaderSettings.ConformanceLevel = ConformanceLevel.Fragment;
            //string xmlData = "<Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_ComputerSystem\" ParentClass=\"CCM_ComputerSystem\"><CCM_ComputerSystem><Domain>APERTURE</Domain></CCM_ComputerSystem></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_Client\" ParentClass=\"CCM_Client\"><CCM_Client><ClientIdChangeDate>12/09/2021 21:56:46</ClientIdChangeDate><ClientVersion>5.00.8325.0000</ClientVersion><PreviousClientId>Unknown</PreviousClientId></CCM_Client></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"SMS_Authority\" ParentClass=\"SMS_Authority\"><SMS_Authority /></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_ADSiteInfo\" ParentClass=\"CCM_ADSiteInfo\"><CCM_ADSiteInfo><ADSiteName>Default-First-Site-Name</ADSiteName></CCM_ADSiteInfo></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_ExtNetworkAdapterConfiguration\" ParentClass=\"CCM_ExtNetworkAdapterConfiguration\"><CCM_ExtNetworkAdapterConfiguration><FQDN>CAVE-JOHNSON-PC.APERTURE</FQDN></CCM_ExtNetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"Win32_ComputerSystemProduct\" ParentClass=\"Win32_ComputerSystemProduct\"><Win32_ComputerSystemProduct><IdentifyingNumber>VMware-56 4d 42 99 ed 81 7b 88-75 78 61 4c 79 c0 6e 1b</IdentifyingNumber><Name>VMware Virtual Platform</Name><UUID>99424D56-81ED-887B-7578-614C79C06E1B</UUID><Version>None</Version></Win32_ComputerSystemProduct></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_DiscoveryData\" ParentClass=\"CCM_DiscoveryData\"><CCM_DiscoveryData><PlatformID>Microsoft Windows NT Server 10.0</PlatformID></CCM_DiscoveryData></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"CCM_NetworkAdapterConfiguration\" ParentClass=\"CCM_NetworkAdapterConfiguration\"><CCM_NetworkAdapterConfiguration><IPSubnet>192.168.57.0</IPSubnet><IPSubnet>254.128.0.0</IPSubnet></CCM_NetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\CAVE-JOHNSON-PC\\root\\ccm\" Class=\"Win32_NetworkAdapterConfiguration\" ParentClass=\"Win32_NetworkAdapterConfiguration\"><Win32_NetworkAdapterConfiguration><IPAddress>192.168.57.101</IPAddress><IPAddress>fe80::9d04:12d5:b6cd:c139</IPAddress><Index>1</Index><MACAddress>00:50:56:2B:CD:37</MACAddress></Win32_NetworkAdapterConfiguration></Instance>";
            string xmlData = "<Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_ExtNetworkAdapterConfiguration\" ParentClass=\"CCM_ExtNetworkAdapterConfiguration\"><CCM_ExtNetworkAdapterConfiguration><FQDN>test-yo.local</FQDN></CCM_ExtNetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_DiscoveryData\" ParentClass=\"CCM_DiscoveryData\"><CCM_DiscoveryData><PlatformID>Microsoft Windows NT Workstation 2010.0</PlatformID></CCM_DiscoveryData></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"CCM_NetworkAdapterConfiguration\" ParentClass=\"CCM_NetworkAdapterConfiguration\"><CCM_NetworkAdapterConfiguration><IPSubnet>192.168.57.0</IPSubnet><IPSubnet>254.128.0.0</IPSubnet></CCM_NetworkAdapterConfiguration></Instance><Instance Content=\"New\" Namespace=\"\\\\test-yo\\root\\ccm\" Class=\"Win32_NetworkAdapterConfiguration\" ParentClass=\"Win32_NetworkAdapterConfiguration\"><Win32_NetworkAdapterConfiguration><IPAddress>192.168.57.101</IPAddress><IPAddress>fe80::9d04:12d5:b6cd:c139</IPAddress><Index>1</Index><MACAddress>00:50:56:2B:CD:37</MACAddress></Win32_NetworkAdapterConfiguration></Instance>";
            XmlReader xmlReader = XmlReader.Create(new StringReader(xmlData), xmlReaderSettings);
            ddrMessage.InventoryReport.ReportBody.ReadXml(xmlReader);
            Console.WriteLine(ddrMessage.InventoryReport.ReportBody.RawXml);
            
            //XmlReader xmlReader = XmlReader.Create(new StringReader(ddrMessage.InventoryReport.ReportBody.RawXml), xmlReaderSettings);
            //xmlReader.MoveToContent();
            //xmlReader.ReadToFollowing("PlatformID");


            //while (xmlReader.Read())
            //{
             //   Console.WriteLine("> ".PadRight(xmlReader.Depth*4) + xmlReader.Name + xmlReader.Value);
            //}

            
            //inventoryReport.ReportBody.ReadXml(xmlReader);


            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();



         
        }
    }
}
