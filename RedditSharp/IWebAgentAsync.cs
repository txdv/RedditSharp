using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RedditSharp
{
    public interface IWebAgentAsync : IWebAgentBase
    {
        Task<HttpWebRequest> CreateRequestAsync(string url, string method);
        Task<HttpWebRequest> CreateGetAsync(string url);
        Task<HttpWebRequest> CreatePostAsync(string url);
        Task<string> GetResponseStringAsync(Stream stream);
        Task WritePostBodyAsync(Stream stream, object data, params string[] additionalFields);
        Task<JToken> CreateAndExecuteRequestAsync(string url);
        Task<JToken> ExecuteRequestAsync(HttpWebRequest request);
    }
}

