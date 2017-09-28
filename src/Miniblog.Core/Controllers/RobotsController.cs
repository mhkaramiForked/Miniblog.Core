﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Atom;
using Microsoft.SyndicationFeed.Rss;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Miniblog.Core
{
    public class RobotsController : Controller
    {
        private IBlogStorage _storage;
        private IOptionsSnapshot<BlogSettings> _settings;
        private RenderingService _rs;

        public RobotsController(IBlogStorage storage, IOptionsSnapshot<BlogSettings> settings, RenderingService rs)
        {
            _storage = storage;
            _settings = settings;
            _rs = rs;
        }

        [Route("/robots.txt")]
        public string RobotsTxt()
        {
            string host = Request.Scheme + "://" + Request.Host;
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow: /posts");
            sb.AppendLine($"sitemap: {host}/sitemap.xml");

            return sb.ToString();
        }

        [Route("/sitemap.xml")]
        public void SitemapXml()
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "application/xml";

            using (var xml = XmlWriter.Create(Response.Body, new XmlWriterSettings { Indent = true }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                var posts = _storage.GetPosts(int.MaxValue);

                foreach (Post post in posts)
                {
                    xml.WriteStartElement("url");
                    xml.WriteElementString("loc", host + post.GetLink());
                    xml.WriteElementString("lastmod", post.LastModified.ToString("yyyy-MM-ddThh:mmzzz"));
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }
        }

        [Route("/rsd.xml")]
        public void RsdXml()
        {
            string host = Request.Scheme + "://" + Request.Host;

            Response.ContentType = "application/xml";

            using (var xml = XmlWriter.Create(Response.Body, new XmlWriterSettings { Indent = true }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("rsd");
                xml.WriteAttributeString("version", "1.0");

                xml.WriteStartElement("service");

                xml.WriteElementString("enginename", "Miniblog.Core");
                xml.WriteElementString("enginelink", "http://github.com/madskristensen/Miniblog.Core/");
                xml.WriteElementString("homepagelink", host);

                xml.WriteStartElement("apis");
                xml.WriteStartElement("api");
                xml.WriteAttributeString("name", "MetaWeblog");
                xml.WriteAttributeString("preferred", "true");
                xml.WriteAttributeString("apilink", host + "/metaweblog");
                xml.WriteAttributeString("blogid", "1");

                xml.WriteEndElement(); // api
                xml.WriteEndElement(); // apis
                xml.WriteEndElement(); // service
                xml.WriteEndElement(); // rsd
            }
        }

        [Route("/feed/{type}")]
        public async Task Rss(string type)
        {
            Response.ContentType = "application/xml";
            string host = Request.Scheme + "://" + Request.Host;

            using (XmlWriter xmlWriter = XmlWriter.Create(Response.Body, new XmlWriterSettings() { Async = true, Indent = true }))
            {
                var writer = await GetWriter(type, xmlWriter);
                var posts = _storage.GetPosts(10);

                foreach (Post post in posts)
                {
                    var item = new SyndicationItem
                    {
                        Title = post.Title,
                        Description = _rs.RenderMarkdown(post).Value,
                        Id = host + post.GetLink(),
                        Published = post.PubDate,
                        LastUpdated = post.LastModified
                    };

                    foreach (string category in post.Categories)
                    {
                        item.AddCategory(new SyndicationCategory(category));
                    }

                    item.AddContributor(new SyndicationPerson(_settings.Value.Owner, "test@example.com"));
                    item.AddLink(new SyndicationLink(new Uri(item.Id), "self"));

                    await writer.Write(item);
                }
            }
        }

        private async Task<ISyndicationFeedWriter> GetWriter(string type, XmlWriter xmlWriter)
        {
            string host = Request.Scheme + "://" + Request.Host + "/";

            if (type.Equals("rss", StringComparison.OrdinalIgnoreCase))
            {
                var rss = new RssFeedWriter(xmlWriter);
                await rss.WriteTitle(_settings.Value.Name);
                await rss.WriteDescription(_settings.Value.Description);
                await rss.WriteGenerator("Miniblog.Core");
                return rss;
            }

            var atom = new AtomFeedWriter(xmlWriter);
            await atom.WriteTitle(_settings.Value.Name);
            await atom.WriteId(host);
            return atom;
        }
    }
}