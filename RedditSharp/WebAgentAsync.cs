using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RedditSharp
{
    public partial class WebAgent : IWebAgentAsync
    {
        public async Task<JToken> CreateAndExecuteRequestAsync(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                if (!Uri.TryCreate(String.Format("{0}://{1}{2}", Protocol, RootDomain, url), UriKind.Absolute, out uri))
                    throw new Exception("Could not parse Uri");
            }
            var request = CreateGet(uri);
            try { return await ExecuteRequestAsync(request); }
            catch (Exception)
            {
                var tempProtocol = Protocol;
                var tempRootDomain = RootDomain;
                Protocol = "http";
                RootDomain = "www.reddit.com";
                var retval = await CreateAndExecuteRequestAsync(url);
                Protocol = tempProtocol;
                RootDomain = tempRootDomain;
                return retval;
            }
        }

        public async Task<JToken> ExecuteRequestAsync(HttpWebRequest request)
        {
            EnforceRateLimit();
            var response = await request.GetResponseAsync();
            var result = await GetResponseStringAsync(response.GetResponseStream());
            return ExecuteRequestBase(result);
        }

        private static async Task EnforceRateLimitAsync()
        {
            foreach (var timeout in EnforceRateLimitEnumerable()) {
                await Task.Delay(timeout);
            }
        }

        private async Task<HttpWebRequest> CreateRequestAsync(Uri uri, string method)
        {
            await EnforceRateLimitAsync();
            return CreateRequestBase(uri, method);
        }

        public async Task<HttpWebRequest> CreateRequestAsync(string url, string method)
        {
            await EnforceRateLimitAsync();
            return CreateRequestBase(url, method);
        }

        public async Task<HttpWebRequest> CreateGetAsync(string url)
        {
            return await CreateRequestAsync(url, "GET");
        }

        private async Task<HttpWebRequest> CreateGetAsync(Uri url)
        {
            return await CreateRequestAsync(url, "GET");
        }

        public async Task<HttpWebRequest> CreatePostAsync(string url)
        {
            var request = await CreateRequestAsync(url, "POST");
            request.ContentType = "application/x-www-form-urlencoded";
            return request;
        }

        public async Task<string> GetResponseStringAsync(Stream stream)
        {
            var data = await new StreamReader(stream).ReadToEndAsync();
            stream.Close();
            return data;
        }

        public async Task WritePostBodyAsync(Stream stream, object data, params string[] additionalFields)
        {
            var raw = FormatWritePostBody(data, additionalFields);
            await stream.WriteAsync(raw, 0, raw.Length);
            stream.Close();
        }
    }
}

