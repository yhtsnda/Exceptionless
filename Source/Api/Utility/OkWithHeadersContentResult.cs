﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Api.Utility {
    public class OkWithHeadersContentResult<T> : OkNegotiatedContentResult<T> {
        public OkWithHeadersContentResult(T content, IContentNegotiator contentNegotiator, HttpRequestMessage request, IEnumerable<MediaTypeFormatter> formatters) : base(content, contentNegotiator, request, formatters) { }

        public OkWithHeadersContentResult(T content, ApiController controller, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = null)
            : base(content, controller) {
            Headers = headers;
        }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }

        public async override Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken) {
            HttpResponseMessage response = await base.ExecuteAsync(cancellationToken);

            if (Headers != null)
                foreach (var header in Headers)
                    response.Headers.Add(header.Key, header.Value);

            return response;
        }
    }

    public class OkWithResourceLinks<TEntity> : OkWithHeadersContentResult<IList<TEntity>>
        where TEntity : class {
        public OkWithResourceLinks(IList<TEntity> content, IContentNegotiator contentNegotiator, HttpRequestMessage request, IEnumerable<MediaTypeFormatter> formatters) : base(content, contentNegotiator, request, formatters) { }

        public OkWithResourceLinks(IList<TEntity> content, ApiController controller, bool hasMore, int? page = null, Func<TEntity, string> pagePropertyAccessor = null)
            : base(content, controller) {
            if (content == null)
                return;

            List<string> links;
            if (page.HasValue)
                links = GetPagedLinks(page.Value, hasMore);
            else
                links = GetBeforeAndAfterLinks(content, hasMore, pagePropertyAccessor);

            if (links.Count == 0)
                return;

            Headers = new Dictionary<string, IEnumerable<string>> {
                { "Link", links.ToArray() }
            };
        }

        private List<string> GetPagedLinks(int page, bool hasMore) {
            bool includePrevious = page > 1;
            bool includeNext = hasMore;

            var previousParameters = Request.RequestUri.ParseQueryString();
            previousParameters["page"] = (page - 1).ToString();
            var nextParameters = new NameValueCollection(previousParameters);
            nextParameters["page"] = (page + 1).ToString();

            string baseUrl = Request.RequestUri.ToString();
            if (!String.IsNullOrEmpty(Request.RequestUri.Query))
                baseUrl = baseUrl.Replace(Request.RequestUri.Query, "");

            string previousLink = String.Format("<{0}?{1}>; rel=\"previous\"", baseUrl, previousParameters.ToQueryString());
            string nextLink = String.Format("<{0}?{1}>; rel=\"next\"", baseUrl, nextParameters.ToQueryString());

            var links = new List<string>();
            if (includePrevious)
                links.Add(previousLink);
            if (includeNext)
                links.Add(nextLink);

            return links;
        }

        private List<string> GetBeforeAndAfterLinks(IList<TEntity> content, bool hasMore, Func<TEntity, string> pagePropertyAccessor) {
            if (pagePropertyAccessor == null && typeof(IIdentity).IsAssignableFrom(typeof(TEntity)))
                pagePropertyAccessor = e => ((IIdentity)e).Id;

            if (pagePropertyAccessor == null)
                return new List<string>();

            string firstId = content.Any() ? pagePropertyAccessor(content.First()) : String.Empty;
            string lastId = content.Any() ? pagePropertyAccessor(content.Last()) : String.Empty;

            bool includePrevious = true;
            bool includeNext = hasMore;
            bool hasBefore = false;
            bool hasAfter = false;

            var previousParameters = Request.RequestUri.ParseQueryString();
            if (previousParameters["before"] != null)
                hasBefore = true;
            previousParameters.Remove("before");
            if (previousParameters["after"] != null)
                hasAfter = true;
            previousParameters.Remove("after");
            var nextParameters = new NameValueCollection(previousParameters);

            previousParameters.Add("before", firstId);
            nextParameters.Add("after", lastId);

            if (hasBefore && !content.Any()) {
                // are we currently before the first page?
                includePrevious = false;
                includeNext = true;
                nextParameters.Remove("after");
            } else if (!hasBefore && !hasAfter) {
                // are we at the first page?
                includePrevious = false;
            }

            string baseUrl = Request.RequestUri.ToString();
            if (!String.IsNullOrEmpty(Request.RequestUri.Query))
                baseUrl = baseUrl.Replace(Request.RequestUri.Query, "");

            string previousLink = String.Format("<{0}?{1}>; rel=\"previous\"", baseUrl, previousParameters.ToQueryString());
            string nextLink = String.Format("<{0}?{1}>; rel=\"next\"", baseUrl, nextParameters.ToQueryString());

            var links = new List<string>();
            if (includePrevious)
                links.Add(previousLink);
            if (includeNext)
                links.Add(nextLink);

            return links;
        }
    }
}