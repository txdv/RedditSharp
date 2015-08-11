using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace RedditSharp
{
    public interface IWebAgent : IWebAgentBase, IWebAgentAsync
    {
        HttpWebRequest CreateRequest(string url, string method);
        HttpWebRequest CreateGet(string url);
        HttpWebRequest CreatePost(string url);
        string GetResponseString(Stream stream);
        void WritePostBody(Stream stream, object data, params string[] additionalFields);
        JToken CreateAndExecuteRequest(string url);
        JToken ExecuteRequest(HttpWebRequest request);
    }
}
