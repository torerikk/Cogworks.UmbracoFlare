﻿using System.Xml.Serialization;

namespace Cogworks.UmbracoFlare.Core.Models
{
    [XmlRoot(Namespace = "cloudflare", ElementName = "cloudflare")]
    public class CloudflareConfigModel
    {
        [XmlElement(ElementName = "purgeCacheOn")]
        public bool PurgeCacheOn { get; set; }

        [XmlElement(ElementName = "apiKey", IsNullable = true)]
        public string ApiKey { get; set; }

        [XmlElement(ElementName = "accountEmail", IsNullable = true)]
        public string AccountEmail { get; set; }

        [XmlElement(ElementName = "credentialsAreValid")]
        public bool CredentialsAreValid { get; set; }
    }
}