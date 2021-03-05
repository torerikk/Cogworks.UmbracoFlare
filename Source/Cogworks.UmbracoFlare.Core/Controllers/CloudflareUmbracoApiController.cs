﻿using Cogworks.UmbracoFlare.Core.Extensions;
using Cogworks.UmbracoFlare.Core.Helpers;
using Cogworks.UmbracoFlare.Core.Models;
using Cogworks.UmbracoFlare.Core.Models.Api;
using Cogworks.UmbracoFlare.Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Http;
using Cogworks.UmbracoFlare.Core.Constants;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Cogworks.UmbracoFlare.Core.Controllers
{
    [PluginController("UmbracoFlare")]
    public class CloudflareUmbracoApiController : UmbracoAuthorizedApiController
    {
        private readonly ICloudflareService _cloudflareService;
        private readonly IUmbracoFlareDomainService _umbracoFlareDomainService;
        private readonly IConfigurationService _configurationService;

        public CloudflareUmbracoApiController(ICloudflareService cloudflareService, IUmbracoFlareDomainService umbracoFlareDomainService,
            IConfigurationService configurationService)
        {
            _cloudflareService = cloudflareService;
            _umbracoFlareDomainService = umbracoFlareDomainService;
            _configurationService = configurationService;
        }

        [HttpGet]
        public UmbracoFlareConfigModel GetConfig()
        {
            var configurationFile = _configurationService.LoadConfigurationFile();
            if (!_configurationService.ConfigurationFileHasData(configurationFile))
            {
                return configurationFile;
            }

            var userDetails = _cloudflareService.GetCloudflareUserDetails(configurationFile);
            configurationFile.CredentialsAreValid = userDetails != null && userDetails.Success;

            return configurationFile;
        }

        [HttpPost]
        public UmbracoFlareConfigModel UpdateConfigStatus([FromBody] UmbracoFlareConfigModel config)
        {
            var userDetails = _cloudflareService.GetCloudflareUserDetails(config);
            config.CredentialsAreValid = userDetails != null && userDetails.Success;

            var configurationFile = _configurationService.SaveConfigurationFile(config);

            return configurationFile;
        }

        [HttpPost]
        public StatusWithMessage PurgeAll()
        {
            var domains = _umbracoFlareDomainService.GetAllowedCloudflareDomains();
            var results = domains.Select(domain => _cloudflareService.PurgeEverything(domain)).ToList();

            return new StatusWithMessage { Success = results.All(x => x.Success), Message = _cloudflareService.PrintResultsSummary(results) };
        }

        [HttpGet]
        public IEnumerable<string> GetAllowedDomains()
        {
            return _umbracoFlareDomainService.GetAllowedCloudflareDomains();
        }

        [HttpPost]
        public StatusWithMessage PurgeStaticFiles([FromBody] PurgeStaticFilesRequestModel model)
        {
            if (!model.StaticFiles.HasAny())
            {
                return new StatusWithMessage(false, "There were not static files selected to purge");
            }

            var results = new List<StatusWithMessage>();
            var fullUrlsToPurge = new List<string>();
            var allFilePaths = _cloudflareService.GetFilePaths(model.StaticFiles);

            foreach (var filePath in allFilePaths)
            {
                var extension = Path.GetExtension(filePath);

                if (ApplicationConstants.AllowedFileExtensions.Contains(extension))
                {
                    var urls = UrlHelper.GetFullUrlForPurgeStaticFiles(filePath, model.SelectedDomains, true);
                    fullUrlsToPurge.AddRange(urls);
                }
            }

            var pageStatusMessages = _cloudflareService.PurgePages(fullUrlsToPurge);
            results.AddRange(pageStatusMessages);

            if (results.Any(x => !x.Success))
            {
                var resultsSummary = _cloudflareService.PrintResultsSummary(results);
                return new StatusWithMessage(false, resultsSummary);
            }

            return new StatusWithMessage(true, $"{results.Count(x => x.Success)} static files purged successfully.");
        }

        [HttpPost]
        public StatusWithMessage PurgeCacheForUrls([FromBody] PurgeUrlsRequestModel model)
        {
            if (!model.Urls.HasAny())
            {
                return new StatusWithMessage(false, "You must provide urls to clear the cache for.");
            }

            var builtUrls = new List<string>();
            
            if (model.Domains.HasAny())
            {
                builtUrls.AddRange(UrlHelper.MakeFullUrlsWithDomain(model.Urls, model.Domains, true));
            }

            var urlsWithWildCards = builtUrls.Where(x => x.Contains('*'));
            var willCardsUrls = !urlsWithWildCards.HasAny()
                ? builtUrls
                : _umbracoFlareDomainService.GetAllUrlsForWildCardUrls(urlsWithWildCards);

            builtUrls.AddRangeUnique(willCardsUrls);
            
            var results = _cloudflareService.PurgePages(builtUrls);

            if (results.Any(x => !x.Success))
            {
                return new StatusWithMessage(false, _cloudflareService.PrintResultsSummary(results));
            }

            return new StatusWithMessage(true, $"{results.Count(x => x.Success)} urls purged successfully.");
        }

        [HttpPost]
        public StatusWithMessage PurgeCacheForContentNode([FromBody] PurgeFromContentTree model)
        {
            if (model.NodeId <= 0)
            {
                return new StatusWithMessage(false, "You must provide a node id.");
            }
            
            var urls = new List<string>();
            urls.AddRange(_umbracoFlareDomainService.GetUrlsForNode(model.NodeId, model.PurgeChildren));
            
            var results = _cloudflareService.PurgePages(urls);

            if (results.Any(x => !x.Success))
            {
                return new StatusWithMessage(false, _cloudflareService.PrintResultsSummary(results));
            }

            return new StatusWithMessage(true, $"{results.Count(x => x.Success)} urls purged successfully.");
        }
    }
}